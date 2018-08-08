using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Indexing;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Linq.Solr;
using System;
using System.Collections.Generic;

namespace Sitecore.ContentSearch.Linq.Solr
{
  public class SolrIndex<TItem> : Index<TItem, SolrCompositeQuery>
  {
    private readonly SolrQueryOptimizer queryOptimizer = new SolrQueryOptimizer();

    private readonly QueryMapper<SolrCompositeQuery> queryMapper;

    private readonly SolrIndexParameters parameters;

    protected override QueryMapper<SolrCompositeQuery> QueryMapper => queryMapper;

    protected override IQueryOptimizer QueryOptimizer => queryOptimizer;

    protected override FieldNameTranslator FieldNameTranslator => parameters.FieldNameTranslator;

    protected override IIndexValueFormatter ValueFormatter => parameters.ValueFormatter;

    public SolrIndexParameters Parameters => parameters;

    public SolrIndex(SolrIndexParameters parameters)
    {
      if (parameters == null)
      {
        throw new ArgumentNullException("parameters");
      }
      queryMapper = new SolrQueryMapper(parameters);
      this.parameters = parameters;
    }

    public override TResult Execute<TResult>(SolrCompositeQuery compositeQuery)
    {
      return default(TResult);
    }

    public override IEnumerable<TElement> FindElements<TElement>(SolrCompositeQuery compositeQuery)
    {
      return new List<TElement>();
    }
  }
}