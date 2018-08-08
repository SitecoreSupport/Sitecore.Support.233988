using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Maintenance;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.SolrProvider;
using System;
using System.Runtime.InteropServices;

namespace Sitecore.Support.ContentSearch.SolrProvider
{
  public class SolrSearchIndex : Sitecore.ContentSearch.SolrProvider.SolrSearchIndex
  {
    public SolrSearchIndex(string name, string core, IIndexPropertyStore propertyStore) : base(name, core, propertyStore)
    {
    }

    public SolrSearchIndex(string name, string core, IIndexPropertyStore propertyStore, string group) : base(name, core, propertyStore, group)
    {
    }

    public override IProviderSearchContext CreateSearchContext(SearchSecurityOptions options = SearchSecurityOptions.Default) =>
        new Sitecore.Support.ContentSearch.SolrProvider.SolrSearchContext(this, options);
  }
}
