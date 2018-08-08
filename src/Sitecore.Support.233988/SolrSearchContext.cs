using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Indexing;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.Pipelines.QueryGlobalFilters;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.Exceptions;
using SolrNet.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Sitecore.ContentSearch.SolrProvider
{
  public class SolrSearchContext : IProviderSearchContext, IDisposable
  {
    private readonly SolrSearchIndex index;

    private readonly SearchSecurityOptions securityOptions;

    private readonly IContentSearchConfigurationSettings contentSearchSettings;

    private Sitecore.ContentSearch.Abstractions.ISettings settings;

    private bool? convertQueryDatesToUtc;

    public ISearchIndex Index => index;

    public bool ConvertQueryDatesToUtc
    {
      get
      {
        if (convertQueryDatesToUtc.HasValue)
        {
          return convertQueryDatesToUtc.Value;
        }
        return contentSearchSettings.ConvertQueryDatesToUtc;
      }
      set
      {
        convertQueryDatesToUtc = value;
      }
    }

    public SearchSecurityOptions SecurityOptions => securityOptions;

    public SolrSearchContext(SolrSearchIndex index, SearchSecurityOptions options = SearchSecurityOptions.Default)
    {
      Assert.ArgumentNotNull(index, "index");
      Assert.ArgumentNotNull(options, "options");
      if (options == SearchSecurityOptions.Default)
      {
        options = index.Configuration.DefaultSearchSecurityOption;
      }
      this.index = index;
      contentSearchSettings = this.index.Locator.GetInstance<IContentSearchConfigurationSettings>();
      settings = this.index.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
      securityOptions = options;
    }

    public IQueryable<TItem> GetQueryable<TItem>()
    {
      return GetQueryable<TItem>(new IExecutionContext[0]);
    }

    public IQueryable<TItem> GetQueryable<TItem>(IExecutionContext executionContext)
    {
      return GetQueryable<TItem>(new IExecutionContext[1]
      {
            executionContext
      });
    }

    public virtual IQueryable<TItem> GetQueryable<TItem>(params IExecutionContext[] executionContexts)
    {
      if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsIndexOn(index.Name))
      {
        return new List<TItem>().AsEnumerable().AsQueryable();
      }
      LinqToSolrIndex<TItem> linqToSolrIndex = new LinqToSolrIndex<TItem>(this, executionContexts);
      if (contentSearchSettings.EnableSearchDebug())
      {
        ((IHasTraceWriter)linqToSolrIndex).TraceWriter = new LoggingTraceWriter(SearchLog.Log);
      }
      IQueryable<TItem> result = ((Index<TItem, SolrCompositeQuery>)linqToSolrIndex).GetQueryable();
      if (typeof(SearchResultItem).IsAssignableFrom(typeof(TItem)))
      {
        QueryGlobalFiltersArgs queryGlobalFiltersArgs = new QueryGlobalFiltersArgs(((Index<TItem, SolrCompositeQuery>)linqToSolrIndex).GetQueryable(), typeof(TItem), executionContexts.ToList());
        Index.Locator.GetInstance<Sitecore.Abstractions.ICorePipeline>().Run("contentSearch.getGlobalLinqFilters", queryGlobalFiltersArgs);
        result = (IQueryable<TItem>)queryGlobalFiltersArgs.Query;
      }
      return result;
    }

    public IEnumerable<SearchIndexTerm> GetTermsByFieldName(string fieldName, string filter)
    {
      SolrSearchFieldConfiguration solrSearchFieldConfiguration = index.Configuration.FieldMap.GetFieldConfiguration(fieldName.ToLowerInvariant()) as SolrSearchFieldConfiguration;
      if (solrSearchFieldConfiguration != null)
      {
        fieldName = solrSearchFieldConfiguration.FormatFieldName(fieldName, Index.Schema, null, settings.DefaultLanguage());
      }
      IEnumerable<SearchIndexTerm> result = new HashSet<SearchIndexTerm>();
      try
      {
        result = GetFacets(fieldName, filter);
        return result;
      }
      catch (Exception ex)
      {
        if (!(ex is SolrConnectionException) && !(ex is SolrNetException))
        {
          throw;
        }
        string message = ex.Message;
        if (ex.Message.StartsWith("<?xml"))
        {
          XmlDocument xmlDocument = new XmlDocument();
          xmlDocument.LoadXml(ex.Message);
          XmlNode xmlNode = xmlDocument.SelectSingleNode("/response/lst[@name='error'][1]/str[@name='msg'][1]");
          XmlNode xmlNode2 = xmlDocument.SelectSingleNode("/response/lst[@name='responseHeader'][1]/lst[@name='params'][1]/str[@name='q'][1]");
          if (xmlNode != null && xmlNode2 != null)
          {
            message = $"Solr Error : [\"{xmlNode.InnerText}\"] - Term Query attempted: [{xmlNode2.InnerText}]";
            SearchLog.Log.Error(message, null);
            return result;
          }
        }
        Log.Error(message, this);
        return result;
      }
    }

    private IEnumerable<SearchIndexTerm> GetTerms(string fieldName, string filter)
    {
      TermsParameters termsParameters = new TermsParameters(fieldName)
      {
        Sort = TermsSort.Count
      };
      if (!string.IsNullOrEmpty(filter))
      {
        termsParameters.Prefix = filter;
      }
      HashSet<SearchIndexTerm> hashSet = new HashSet<SearchIndexTerm>();
      foreach (KeyValuePair<string, int> item in index.SolrOperations.Query(SolrQuery.All, new QueryOptions
      {
        Terms = termsParameters,
        Rows = new int?(0)
      }).Terms.SelectMany((TermsResult termsResult) => termsResult.Terms))
      {
        hashSet.Add(new SearchIndexTerm(item.Key, () => item.Value));
      }
      return hashSet;
    }

    private IEnumerable<SearchIndexTerm> GetFacets(string fieldName, string filter)
    {
      HashSet<SearchIndexTerm> hashSet = new HashSet<SearchIndexTerm>();
      QueryOptions queryOptions = new QueryOptions();
      queryOptions.AddFacets(new SolrFacetFieldQuery(fieldName));
      if (!string.IsNullOrEmpty(filter))
      {
        queryOptions.Facet.Prefix = filter;
      }
      queryOptions.Facet.Sort = true;
      queryOptions.Rows = 0;
      foreach (KeyValuePair<string, int> item in index.SolrOperations.Query(SolrQuery.All, queryOptions).FacetFields[fieldName])
      {
        hashSet.Add(new SearchIndexTerm(item.Key, () => item.Value));
      }
      return hashSet;
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);
    }
  }
}