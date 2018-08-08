using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Indexing;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.Pipelines.QueryGlobalFilters;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sitecore.Support.ContentSearch.SolrProvider
{
  public class SolrSearchContext : Sitecore.ContentSearch.SolrProvider.SolrSearchContext, IProviderSearchContext, IDisposable
  {
    public SolrSearchContext(Sitecore.ContentSearch.SolrProvider.SolrSearchIndex index, SearchSecurityOptions options = SearchSecurityOptions.Default) : base(index, options)
    {
    }

    public new IQueryable<TItem> GetQueryable<TItem>() =>
        this.GetQueryable<TItem>(new IExecutionContext[0]);

    public new IQueryable<TItem> GetQueryable<TItem>(IExecutionContext executionContext)
    {
      IExecutionContext[] executionContexts = new IExecutionContext[] { executionContext };
      return this.GetQueryable<TItem>(executionContexts);
    }

    public new IQueryable<TItem> GetQueryable<TItem>(params IExecutionContext[] executionContexts)
    {
      if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsIndexOn(this.Index.Name))
      {
        return new List<TItem>().AsEnumerable().AsQueryable();
      }
      LinqToSolrIndex<TItem> linqToSolrIndex = new Sitecore.Support.ContentSearch.SolrProvider.LinqToSolrIndex<TItem>(this, executionContexts);
      if (EnableSearchDebug)
      {
        ((IHasTraceWriter)linqToSolrIndex).TraceWriter = new LoggingTraceWriter(SearchLog.Log);
      }
      IQueryable<TItem> result = ((Index<TItem, SolrCompositeQuery>)linqToSolrIndex).GetQueryable();
      if (typeof(SearchResultItem).IsAssignableFrom(typeof(TItem)))
      {
        QueryGlobalFiltersArgs queryGlobalFiltersArgs = new QueryGlobalFiltersArgs(((Index<TItem, SolrCompositeQuery>)linqToSolrIndex).GetQueryable(), typeof(TItem), executionContexts.ToList());
        Index.Locator.GetInstance<ICorePipeline>().Run("contentSearch.getGlobalLinqFilters", queryGlobalFiltersArgs);
        result = (IQueryable<TItem>)queryGlobalFiltersArgs.Query;
      }
      return result;
    }

    public static bool EnableSearchDebug =>
        ContentSearchManager.Locator.GetInstance<IContentSearchConfigurationSettings>().EnableSearchDebug();
  }
}