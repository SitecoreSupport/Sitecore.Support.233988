using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.Support.ContentSearch.Linq.Solr;
using SolrNet;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sitecore.Support.ContentSearch.SolrProvider
{
  public class LinqToSolrIndex<TItem> : Sitecore.ContentSearch.SolrProvider.LinqToSolrIndex<TItem>
  {
    private readonly QueryMapper<SolrCompositeQuery> queryMapper;

    public LinqToSolrIndex(SolrSearchContext context, IExecutionContext executionContext)
    : this(context, (IExecutionContext[])new IExecutionContext[1]
    {
            executionContext
    })
    {
    }

    public LinqToSolrIndex(Sitecore.Support.ContentSearch.SolrProvider.SolrSearchContext context, IExecutionContext[] executionContexts) : base(context, executionContexts)
    {
      this.queryMapper = new Sitecore.Support.ContentSearch.Linq.Solr.SolrQueryMapper(new SolrIndexParameters(context.Index.Configuration.IndexFieldStorageValueFormatter, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator, executionContexts, context.Index.Configuration.FieldMap, context.ConvertQueryDatesToUtc));
    }

    private TResult ApplyScalarMethods<TResult, TDocument>(SolrCompositeQuery compositeQuery, object processedResults, SolrQueryResults<Dictionary<string, object>> results)
    {
      Type type = typeof(TResult).GetGenericArguments()[0];
      Type[] typeArguments = new Type[] { typeof(TResult), type };
      object[] parameters = new object[] { compositeQuery, processedResults, results };
      return (TResult)typeof(Sitecore.ContentSearch.SolrProvider.LinqToSolrIndex<TItem>).GetMethod("ApplyScalarMethods", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(typeArguments).Invoke(this, parameters);
    }

    protected override QueryMapper<SolrCompositeQuery> QueryMapper =>
        this.queryMapper;
  }
}