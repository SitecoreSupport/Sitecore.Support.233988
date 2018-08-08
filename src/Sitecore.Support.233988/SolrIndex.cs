using Sitecore.ContentSearch.Linq.Solr;
using System;
using System.Reflection;

namespace Sitecore.Support.ContentSearch.Linq.Solr
{
  public class SolrIndex<TItem> : Sitecore.ContentSearch.Linq.Solr.SolrIndex<TItem>
  {
    public SolrIndex(SolrIndexParameters parameters) : base(parameters)
    {
      typeof(Sitecore.ContentSearch.Linq.Solr.SolrIndex<TItem>).GetField("queryMapper", BindingFlags.NonPublic).SetValue(this, new Sitecore.Support.ContentSearch.Linq.Solr.SolrQueryMapper(parameters));
    }
  }
}