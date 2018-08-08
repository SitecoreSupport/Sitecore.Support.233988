using Microsoft.Practices.ServiceLocation;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Maintenance;
using Sitecore.ContentSearch.Maintenance.Strategies;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Sharding;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.SolrProvider.Sharding;
using Sitecore.ContentSearch.SolrProvider.SolrNetIntegration;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.Exceptions;
using SolrNet.Impl;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;

namespace Sitecore.ContentSearch.SolrProvider
{
  public class SolrSearchIndex : AbstractSearchIndex, ISearchIndexSwitch
  {
    private readonly HashSet<IIndexUpdateStrategy> strategies = new HashSet<IIndexUpdateStrategy>();

    private SolrIndexSchema schema;

    private SolrIndexSummary summary;

    private ISolrOperations<Dictionary<string, object>> solrOperations;

    internal ISolrCoreAdmin solrAdmin;

    private AbstractFieldNameTranslator fieldNameTranslator;

    private readonly string name;

    private IShardFactory shardFactory = new SolrShardFactory();

    private bool isSharded;

    public virtual ICommitPolicyExecutor CommitPolicyExecutor
    {
      get;
      set;
    }

    public override string Name => name;

    public override IIndexPropertyStore PropertyStore
    {
      get;
      set;
    }

    public override AbstractFieldNameTranslator FieldNameTranslator
    {
      get
      {
        return fieldNameTranslator;
      }
      set
      {
        if (value == null)
        {
          throw new ArgumentNullException("value");
        }
        fieldNameTranslator = value;
      }
    }

    public override ProviderIndexConfiguration Configuration
    {
      get;
      set;
    }

    public override IIndexOperations Operations => new SolrIndexOperations(this);

    public override bool EnableItemLanguageFallback
    {
      get;
      set;
    }

    public override bool EnableFieldLanguageFallback
    {
      get;
      set;
    }

    public override bool IsSharded => isSharded;

    public override IShardingStrategy ShardingStrategy
    {
      get;
      set;
    }

    public override IShardFactory ShardFactory => shardFactory;

    public override IEnumerable<Shard> Shards
    {
      get
      {
        yield break;
      }
    }

    public override ISearchIndexSummary Summary
    {
      get
      {
        if (summary == null && solrAdmin != null)
        {
          CoreResult coreAdmin = RequestStatus();
          summary = new SolrIndexSummary(coreAdmin, this);
        }
        return summary;
      }
    }

    public override ISearchIndexSchema Schema => schema;

    public virtual string Core
    {
      get;
      private set;
    }

    internal ISolrOperations<Dictionary<string, object>> SolrOperations
    {
      get
      {
        if (solrOperations == null)
        {
          Log.Error("Solr operations unavailable. Please check your global.asax, settings and include files.", this);
          throw new ProviderConfigurationException("Solr operations unavailable. Please check your global.asax, settings and include files.");
        }
        return solrOperations;
      }
      set
      {
        solrOperations = value;
      }
    }

    public virtual DateTime LastUpdatedStamp => Summary.LastUpdated;

    public SolrSearchIndex(string name, string core, IIndexPropertyStore propertyStore, string group)
        : base(group)
    {
      Assert.ArgumentNotNull(name, "name");
      Assert.ArgumentNotNull(core, "core");
      Assert.ArgumentNotNull(propertyStore, "propertyStore");
      this.name = name;
      Core = core;
      PropertyStore = propertyStore;
    }

    public SolrSearchIndex(string name, string core, IIndexPropertyStore propertyStore)
        : this(name, core, propertyStore, null)
    {
    }

    public override void AddCrawler(IProviderCrawler crawler)
    {
      Assert.ArgumentNotNull(crawler, "crawler cannot be null");
      base.AddCrawler(crawler, base.initialized);
    }

    public override void AddStrategy(IIndexUpdateStrategy strategy)
    {
      Assert.IsNotNull(strategy, "The strategy cannot be null");
      strategies.Add(strategy);
    }

    public override void Rebuild()
    {
      Rebuild(true, true);
    }

    public override void Rebuild(IndexingOptions indexingOptions)
    {
      PerformRebuild(true, true, indexingOptions, CancellationToken.None);
    }

    public override void Update(IIndexableUniqueId indexableUniqueId)
    {
      PerformUpdate(indexableUniqueId, IndexingOptions.Default);
    }

    public override void Update(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
    {
      PerformUpdate(indexableUniqueId, indexingOptions);
    }

    public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds)
    {
      PerformUpdate(indexableUniqueIds, IndexingOptions.Default);
    }

    public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds, IndexingOptions indexingOptions)
    {
      PerformUpdate(indexableUniqueIds, indexingOptions);
    }

    public override void Refresh(IIndexable indexableStartingPoint)
    {
      PerformRefresh(indexableStartingPoint, IndexingOptions.Default, CancellationToken.None);
    }

    public override void Refresh(IIndexable indexableStartingPoint, IndexingOptions indexingOptions)
    {
      PerformRefresh(indexableStartingPoint, indexingOptions, CancellationToken.None);
    }

    public override void Delete(IIndexableId indexableId)
    {
      PerformDelete(indexableId, IndexingOptions.Default);
    }

    public override void Delete(IIndexableId indexableId, IndexingOptions indexingOptions)
    {
      PerformDelete(indexableId, indexingOptions);
    }

    public override void Delete(IIndexableUniqueId indexableUniqueId)
    {
      PerformDelete(indexableUniqueId, IndexingOptions.Default);
    }

    public override void Delete(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
    {
      PerformDelete(indexableUniqueId, indexingOptions);
    }

    public override void Initialize()
    {
      if (PropertyStore == null)
      {
        throw new ConfigurationErrorsException("Index PropertyStore have not been configured.");
      }
      solrOperations = ServiceLocator.Current.GetInstance<ISolrOperations<Dictionary<string, object>>>(Core);
      solrAdmin = (SolrContentSearchManager.SolrAdmin as ISolrCoreAdminEx);
      bool flag = false;
      if (solrAdmin != null)
      {
        try
        {
          CoreResult coreAdmin = RequestStatus();
          summary = new SolrIndexSummary(coreAdmin, this);
          schema = new SolrIndexSchema(SolrOperations.GetSchema());
        }
        catch (Exception ex)
        {
          if (ex is SolrConnectionException)
          {
            Log.Error($"Unable to connect to [{SolrContentSearchManager.SolrSettings.ServiceAddress()}], Core: [{Core}]", ex, this);
          }
          else
          {
            Log.Error(ex.Message, ex, this);
          }
          flag = true;
        }
        if (flag)
        {
          CrawlingLog.Log.Warn($"Failed to initialize '{Name}' index. Registering the index for re-initialization once connection to SOLR becomes available ...", null);
          SolrStatus.SetIndexForInitialization(this);
          CrawlingLog.Log.Warn("DONE", null);
          return;
        }
      }
      base.InitializeSearchIndexInitializables(Configuration, base.Crawlers, strategies, ShardingStrategy);
      if (Configuration is SolrIndexConfiguration)
      {
        fieldNameTranslator = new SolrFieldNameTranslator(this);
        if (CommitPolicyExecutor == null)
        {
          CommitPolicyExecutor = new NullCommitPolicyExecutor();
        }
        base.CheckInvalidConfiguration();
        isSharded = (ShardingStrategy != null);
        base.initialized = true;
      }
    }

    public override IProviderUpdateContext CreateUpdateContext()
    {
      ICommitPolicyExecutor commitPolicyExecutor = (ICommitPolicyExecutor)CommitPolicyExecutor.Clone();
      commitPolicyExecutor.Initialize(this);
      IContentSearchConfigurationSettings instance = base.Locator.GetInstance<IContentSearchConfigurationSettings>();
      if (instance.IndexingBatchModeEnabled())
      {
        return new SolrBatchUpdateContext(this, SolrOperations, instance.IndexingBatchSize(), commitPolicyExecutor);
      }
      return new SolrUpdateContext(this, SolrOperations, CommitPolicyExecutor);
    }

    public override IProviderDeleteContext CreateDeleteContext()
    {
      return new SolrDeleteContext(this, SolrOperations);
    }

    public override IProviderSearchContext CreateSearchContext(SearchSecurityOptions options = SearchSecurityOptions.Default)
    {
      if (Group == IndexGroup.Experience)
      {
        return new SolrAnalyticsSearchContext(this, options);
      }
      return new SolrSearchContext(this, options);
    }

    public virtual long IndexOnlyDocumentCount()
    {
      SolrQueryByField query = new SolrQueryByField("_indexname", Name);
      return SolrOperations.Query(query, new QueryOptions
      {
        Rows = new int?(1)
      }).NumFound;
    }

    [Obsolete("Use SolrSearchIndex.Reset instead")]
    public virtual void ResetIndex()
    {
      Reset();
    }

    public override void Reset()
    {
      SolrQueryByField q = new SolrQueryByField("_indexname", Name);
      SolrOperations.Delete(q);
      SolrOperations.Commit();
    }

    public virtual void Rebuild(bool resetIndex = true, bool optimizeOnComplete = true)
    {
      PerformRebuild(resetIndex, optimizeOnComplete, IndexingOptions.Default, CancellationToken.None);
    }

    public virtual void OptimizeIndex()
    {
      solrOperations.Optimize();
    }

    protected virtual CoreResult RequestStatus()
    {
      return solrAdmin.Status(Core).Single();
    }

    protected override void PerformRebuild(IndexingOptions indexingOptions, CancellationToken cancellationToken)
    {
      PerformRebuild(true, true, indexingOptions, cancellationToken);
    }

    protected virtual void PerformRebuild(bool resetIndex, bool optimizeOnComplete, IndexingOptions indexingOptions, CancellationToken cancellationToken)
    {
      if (base.ShouldStartIndexing(indexingOptions))
      {
        using (new RebuildIndexingTimer(PropertyStore))
        {
          if (resetIndex)
          {
            Reset();
          }
          using (IProviderUpdateContext providerUpdateContext = CreateUpdateContext())
          {
            foreach (IProviderCrawler crawler in base.Crawlers)
            {
              crawler.RebuildFromRoot(providerUpdateContext, indexingOptions, cancellationToken);
            }
            providerUpdateContext.Commit();
          }
          if (optimizeOnComplete)
          {
            OptimizeIndex();
          }
        }
      }
    }

    private void PerformUpdate(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
    {
      if (base.ShouldStartIndexing(indexingOptions))
      {
        using (IProviderUpdateContext providerUpdateContext = CreateUpdateContext())
        {
          foreach (IProviderCrawler crawler in base.Crawlers)
          {
            crawler.Update(providerUpdateContext, indexableUniqueId, indexingOptions);
          }
          providerUpdateContext.Commit();
        }
      }
    }

    private void PerformDelete(IIndexableId indexableId, IndexingOptions indexingOptions)
    {
      if (base.ShouldStartIndexing(indexingOptions))
      {
        using (IProviderUpdateContext providerUpdateContext = CreateUpdateContext())
        {
          foreach (IProviderCrawler crawler in base.Crawlers)
          {
            crawler.Delete(providerUpdateContext, indexableId, indexingOptions);
          }
          providerUpdateContext.Commit();
        }
      }
    }

    private void PerformDelete(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
    {
      if (base.ShouldStartIndexing(indexingOptions))
      {
        using (IProviderUpdateContext providerUpdateContext = CreateUpdateContext())
        {
          foreach (IProviderCrawler crawler in base.Crawlers)
          {
            crawler.Delete(providerUpdateContext, indexableUniqueId, indexingOptions);
          }
          providerUpdateContext.Commit();
        }
      }
    }
  }
}