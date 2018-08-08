using Microsoft.CSharp.RuntimeBinder;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.Pipelines.GetFacets;
using Sitecore.ContentSearch.Pipelines.ProcessFacets;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.SolrProvider.Logging;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Sitecore.ContentSearch.SolrProvider
{
  public class LinqToSolrIndex<TItem> : SolrIndex<TItem>, ICultureIndex
  {
    private readonly SolrSearchContext context;

    private readonly Sitecore.ContentSearch.Abstractions.ISettings settings;

    private readonly IContentSearchConfigurationSettings contentSearchSettings;

    private readonly Sitecore.Abstractions.ICorePipeline pipeline;

    public LinqToSolrIndex(SolrSearchContext context, IExecutionContext executionContext)
        : this(context, new IExecutionContext[1]
        {
            executionContext
        })
    {
    }

    public LinqToSolrIndex(SolrSearchContext context, IExecutionContext[] executionContexts)
        : base(new SolrIndexParameters(context.Index.Configuration.IndexFieldStorageValueFormatter, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator, executionContexts, context.Index.Configuration.FieldMap, context.ConvertQueryDatesToUtc))
    {
      Assert.ArgumentNotNull(context, "context");
      this.context = context;
      settings = context.Index.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
      contentSearchSettings = context.Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
      pipeline = context.Index.Locator.GetInstance<Sitecore.Abstractions.ICorePipeline>();
    }

    public override TResult Execute<TResult>(SolrCompositeQuery compositeQuery)
    {
      if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(compositeQuery))
      {
        return EnumerableLinq.ExecuteEnumerableLinqQuery<TResult>((IQuery)compositeQuery);
      }
      if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(SearchResults<>))
      {
        Type type = typeof(TResult).GetGenericArguments()[0];
        SolrQueryResults<Dictionary<string, object>> solrQueryResults = this.Execute(compositeQuery, type);
        Type type2 = typeof(SolrSearchResults<>).MakeGenericType(type);
        MethodInfo methodInfo = base.GetType().GetMethod("ApplyScalarMethods", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(TResult), type);
        SelectMethod selectMethod = LinqToSolrIndex<TItem>.GetSelectMethod(compositeQuery);
        object obj = ReflectionUtility.CreateInstance(type2, this.context, solrQueryResults, selectMethod, compositeQuery.ExecutionContexts, compositeQuery.VirtualFieldProcessors);
        return (TResult)methodInfo.Invoke(this, new object[3]
        {
                compositeQuery,
                obj,
                solrQueryResults
        });
      }
      if (typeof(TResult) == typeof(SolrQueryResults<Dictionary<string, object>>))
      {
        return (TResult)Convert.ChangeType(this.Execute(compositeQuery, typeof(SearchResults<>)), typeof(TResult));
      }
      SolrQueryResults<Dictionary<string, object>> solrQueryResults2 = this.Execute(compositeQuery, typeof(TResult));
      SelectMethod selectMethod2 = LinqToSolrIndex<TItem>.GetSelectMethod(compositeQuery);
      SolrSearchResults<TResult> processedResults = new SolrSearchResults<TResult>(this.context, solrQueryResults2, selectMethod2, (IEnumerable<IExecutionContext>)compositeQuery.ExecutionContexts, (IEnumerable<IFieldQueryTranslator>)compositeQuery.VirtualFieldProcessors);
      return ApplyScalarMethods<TResult, TResult>(compositeQuery, processedResults, solrQueryResults2);
    }

    public override IEnumerable<TElement> FindElements<TElement>(SolrCompositeQuery compositeQuery)
    {
      if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(compositeQuery))
      {
        return EnumerableLinq.ExecuteEnumerableLinqQuery<IEnumerable<TElement>>((IQuery)compositeQuery);
      }
      SolrQueryResults<Dictionary<string, object>> searchResults = this.Execute(compositeQuery, typeof(TElement));
      List<SelectMethod> list = Enumerable.ToList<SelectMethod>(Enumerable.Select<QueryMethod, SelectMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Select)), (Func<QueryMethod, SelectMethod>)((QueryMethod m) => (SelectMethod)m)));
      SelectMethod selectMethod = (Enumerable.Count<SelectMethod>((IEnumerable<SelectMethod>)list) == 1) ? list[0] : null;
      return new SolrSearchResults<TElement>(this.context, searchResults, selectMethod, (IEnumerable<IExecutionContext>)compositeQuery.ExecutionContexts, (IEnumerable<IFieldQueryTranslator>)compositeQuery.VirtualFieldProcessors).GetSearchResults();
    }

    internal SolrQueryResults<Dictionary<string, object>> Execute(SolrCompositeQuery compositeQuery, Type resultType)
    {
      if (Enumerable.Any<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods) && Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods).MethodType == QueryMethodType.Union)
      {
        UnionMethod unionMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods) as UnionMethod;
        object innerEnumerable = unionMethod.InnerEnumerable;
        if (<> o__8.<> p__1 == null)
        {

                <> o__8.<> p__1 = CallSite<Func<CallSite, object, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(CSharpBinderFlags.None, "Execute", new Type[1]
                {
                    typeof(SolrQueryResults<Dictionary<string, object>>)
                }, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[2]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                }));
        }
        Func<CallSite, object, object, object> target = <> o__8.<> p__1.Target;
        CallSite<Func<CallSite, object, object, object>> <> p__ = <> o__8.<> p__1;
        object arg = innerEnumerable;
        if (<> o__8.<> p__0 == null)
        {

                <> o__8.<> p__0 = CallSite<Func<CallSite, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.GetMember(CSharpBinderFlags.None, "Expression", typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[1]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                }));
        }
        object obj = target(<> p__, arg, <> o__8.<> p__0.Target(<> o__8.<> p__0, innerEnumerable));
        object outerEnumerable = unionMethod.OuterEnumerable;
        if (<> o__8.<> p__3 == null)
        {

                <> o__8.<> p__3 = CallSite<Func<CallSite, object, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(CSharpBinderFlags.None, "Execute", new Type[1]
                {
                    typeof(SolrQueryResults<Dictionary<string, object>>)
                }, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[2]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                }));
        }
        Func<CallSite, object, object, object> target2 = <> o__8.<> p__3.Target;
        CallSite<Func<CallSite, object, object, object>> <> p__2 = <> o__8.<> p__3;
        object arg2 = outerEnumerable;
        if (<> o__8.<> p__2 == null)
        {

                <> o__8.<> p__2 = CallSite<Func<CallSite, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.GetMember(CSharpBinderFlags.None, "Expression", typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[1]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                }));
        }
        object obj2 = target2(<> p__2, arg2, <> o__8.<> p__2.Target(<> o__8.<> p__2, outerEnumerable));
        if (<> o__8.<> p__5 == null)
        {

                <> o__8.<> p__5 = CallSite<Func<CallSite, object, bool>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.UnaryOperation(CSharpBinderFlags.None, ExpressionType.IsTrue, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[1]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                }));
        }
        Func<CallSite, object, bool> target3 = <> o__8.<> p__5.Target;
        CallSite<Func<CallSite, object, bool>> <> p__3 = <> o__8.<> p__5;
        if (<> o__8.<> p__4 == null)
        {

                <> o__8.<> p__4 = CallSite<Func<CallSite, object, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.BinaryOperation(CSharpBinderFlags.None, ExpressionType.NotEqual, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[2]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant, null)
                }));
        }
        if (target3(<> p__3, <> o__8.<> p__4.Target(<> o__8.<> p__4, obj, null)))
        {
          if (<> o__8.<> p__7 == null)
          {

                    <> o__8.<> p__7 = CallSite<Func<CallSite, object, bool>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.UnaryOperation(CSharpBinderFlags.None, ExpressionType.IsTrue, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[1]
                    {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                    }));
          }
          Func<CallSite, object, bool> target4 = <> o__8.<> p__7.Target;
          CallSite<Func<CallSite, object, bool>> <> p__4 = <> o__8.<> p__7;
          if (<> o__8.<> p__6 == null)
          {

                    <> o__8.<> p__6 = CallSite<Func<CallSite, object, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.BinaryOperation(CSharpBinderFlags.None, ExpressionType.Equal, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[2]
                    {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant, null)
                    }));
          }
          if (target4(<> p__4, <> o__8.<> p__6.Target(<> o__8.<> p__6, obj2, null)))
          {
            if (<> o__8.<> p__8 == null)
            {

                        <> o__8.<> p__8 = CallSite<Func<CallSite, object, SolrQueryResults<Dictionary<string, object>>>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.Convert(CSharpBinderFlags.None, typeof(SolrQueryResults<Dictionary<string, object>>), typeof(LinqToSolrIndex<TItem>)));
            }
            return <> o__8.<> p__8.Target(<> o__8.<> p__8, obj);
          }
          if (<> o__8.<> p__9 == null)
          {

                    <> o__8.<> p__9 = CallSite<Action<CallSite, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(CSharpBinderFlags.ResultDiscarded, "AddRange", null, typeof(LinqToSolrIndex<TItem>), new CSharpArgumentInfo[2]
                    {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                    }));
          }

                <> o__8.<> p__9.Target(<> o__8.<> p__9, obj2, obj);
          if (<> o__8.<> p__10 == null)
          {

                    <> o__8.<> p__10 = CallSite<Func<CallSite, object, SolrQueryResults<Dictionary<string, object>>>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.Convert(CSharpBinderFlags.None, typeof(SolrQueryResults<Dictionary<string, object>>), typeof(LinqToSolrIndex<TItem>)));
          }
          return <> o__8.<> p__10.Target(<> o__8.<> p__10, obj2);
        }
        if (<> o__8.<> p__11 == null)
        {

                <> o__8.<> p__11 = CallSite<Func<CallSite, object, SolrQueryResults<Dictionary<string, object>>>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.Convert(CSharpBinderFlags.None, typeof(SolrQueryResults<Dictionary<string, object>>), typeof(LinqToSolrIndex<TItem>)));
        }
        return <> o__8.<> p__11.Target(<> o__8.<> p__11, obj2 ?? new SolrQueryResults<Dictionary<string, object>>());
      }
      QueryOptions queryOptions = BuildQueryOptions(compositeQuery);
      return GetResult(compositeQuery, queryOptions);
    }

    private QueryOptions BuildQueryOptions(SolrCompositeQuery compositeQuery)
    {
      QueryOptions queryOptions = new QueryOptions();
      if (compositeQuery.Methods != null)
      {
        List<SelectMethod> source = Enumerable.ToList<SelectMethod>(Enumerable.Select<QueryMethod, SelectMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)delegate (QueryMethod m)
        {
          if (m.MethodType == QueryMethodType.Select)
          {
            return Enumerable.Any<string>((IEnumerable<string>)((SelectMethod)m).FieldNames);
          }
          return false;
        }), (Func<QueryMethod, SelectMethod>)((QueryMethod m) => (SelectMethod)m)));
        if (Enumerable.Any<SelectMethod>((IEnumerable<SelectMethod>)source))
        {
          foreach (string item in Enumerable.SelectMany<SelectMethod, string>((IEnumerable<SelectMethod>)source, (Func<SelectMethod, IEnumerable<string>>)((SelectMethod selectMethod) => selectMethod.FieldNames)))
          {
            queryOptions.Fields.Add(item.ToLowerInvariant());
          }
          queryOptions.Fields.Add("_uniqueid");
          queryOptions.Fields.Add("_datasource");
        }
        List<GetResultsMethod> source2 = Enumerable.ToList<GetResultsMethod>(Enumerable.Select<QueryMethod, GetResultsMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.GetResults)), (Func<QueryMethod, GetResultsMethod>)((QueryMethod m) => (GetResultsMethod)m)));
        if (Enumerable.Any<GetResultsMethod>((IEnumerable<GetResultsMethod>)source2))
        {
          if (queryOptions.Fields.Count > 0)
          {
            queryOptions.Fields.Add("score");
          }
          else
          {
            queryOptions.Fields.Add("*");
            queryOptions.Fields.Add("score");
          }
        }
        SortOrder[] sorting = GetSorting(compositeQuery);
        queryOptions.AddOrder(sorting);
        int num;
        int num2;
        GetMaxHits(compositeQuery, contentSearchSettings.SearchMaxResults(), out num, out num2);
        List<SkipMethod> source3 = Enumerable.ToList<SkipMethod>(Enumerable.Select<QueryMethod, SkipMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Skip)), (Func<QueryMethod, SkipMethod>)((QueryMethod m) => (SkipMethod)m)));
        if (Enumerable.Any<SkipMethod>((IEnumerable<SkipMethod>)source3))
        {
          int value = Enumerable.Sum<SkipMethod>((IEnumerable<SkipMethod>)source3, (Func<SkipMethod, int>)((SkipMethod skipMethod) => skipMethod.Count));
          queryOptions.Start = value;
        }
        List<TakeMethod> source4 = Enumerable.ToList<TakeMethod>(Enumerable.Select<QueryMethod, TakeMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Take)), (Func<QueryMethod, TakeMethod>)((QueryMethod m) => (TakeMethod)m)));
        if (Enumerable.Any<TakeMethod>((IEnumerable<TakeMethod>)source4))
        {
          int value2 = Enumerable.Sum<TakeMethod>((IEnumerable<TakeMethod>)source4, (Func<TakeMethod, int>)((TakeMethod takeMethod) => takeMethod.Count));
          queryOptions.Rows = value2;
        }
        if (Enumerable.Any<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Count)) && !ShouldRunCountOnAllDocuments(compositeQuery))
        {
          queryOptions.Rows = 0;
        }
        List<AnyMethod> source5 = Enumerable.ToList<AnyMethod>(Enumerable.Select<QueryMethod, AnyMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Any)), (Func<QueryMethod, AnyMethod>)((QueryMethod m) => (AnyMethod)m)));
        if (compositeQuery.Methods.Count == 1 && Enumerable.Any<AnyMethod>((IEnumerable<AnyMethod>)source5))
        {
          queryOptions.Rows = 0;
        }
        List<GetFacetsMethod> source6 = Enumerable.ToList<GetFacetsMethod>(Enumerable.Select<QueryMethod, GetFacetsMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.GetFacets)), (Func<QueryMethod, GetFacetsMethod>)((QueryMethod m) => (GetFacetsMethod)m)));
        if (compositeQuery.FacetQueries.Count > 0 && (Enumerable.Any<GetFacetsMethod>((IEnumerable<GetFacetsMethod>)source6) || Enumerable.Any<GetResultsMethod>((IEnumerable<GetResultsMethod>)source2)))
        {
          foreach (FacetQuery item2 in EnumerableExtensions.ToHashSet<FacetQuery>(GetFacetsPipeline.Run(pipeline, new GetFacetsArgs(null, compositeQuery.FacetQueries, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator)).FacetQueries))
          {
            if (Enumerable.Any<string>(item2.FieldNames))
            {
              int? minimumResultCount = item2.MinimumResultCount;
              if (Enumerable.Count<string>(item2.FieldNames) == 1)
              {
                SolrFieldNameTranslator solrFieldNameTranslator = FieldNameTranslator as SolrFieldNameTranslator;
                string text = Enumerable.First<string>(item2.FieldNames);
                if (solrFieldNameTranslator != null && text == solrFieldNameTranslator.StripKnownExtensions(text) && context.Index.Configuration.FieldMap.GetFieldConfiguration(text) == null)
                {
                  text = solrFieldNameTranslator.GetIndexFieldName(text.Replace("__", "!").Replace("_", " ").Replace("!", "__"), true);
                }
                queryOptions.AddFacets(new SolrFacetFieldQuery(text)
                {
                  MinCount = minimumResultCount
                });
              }
              if (Enumerable.Count<string>(item2.FieldNames) > 1)
              {
                queryOptions.AddFacets(new SolrFacetPivotQuery
                {
                  Fields = new string[1]
                    {
                                    string.Join(",", item2.FieldNames)
                    },
                  MinCount = minimumResultCount
                });
              }
            }
          }
          if (!Enumerable.Any<GetResultsMethod>((IEnumerable<GetResultsMethod>)source2))
          {
            queryOptions.Rows = 0;
          }
        }
      }
      if (compositeQuery.Filter != null)
      {
        queryOptions.AddFilterQueries(compositeQuery.Filter);
      }
      queryOptions.AddFilterQueries(new SolrQueryByField("_indexname", context.Index.Name));
      return queryOptions;
    }

    private SolrQueryResults<Dictionary<string, object>> GetResult(SolrCompositeQuery compositeQuery, QueryOptions queryOptions)
    {
      List<CultureExecutionContext> cultureContexts = DeterminateCultureContexts(compositeQuery);
      AddCultureToFilterQueries(cultureContexts, queryOptions);
      SolrLoggingSerializer solrLoggingSerializer = new SolrLoggingSerializer();
      string text = solrLoggingSerializer.SerializeQuery(compositeQuery.Query);
      SolrSearchIndex solrSearchIndex = context.Index as SolrSearchIndex;
      try
      {
        if (!queryOptions.Rows.HasValue)
        {
          queryOptions.Rows = contentSearchSettings.SearchMaxResults();
        }
        string message = "Solr Query - ?q=" + text + "&" + string.Join("&", Enumerable.ToArray<string>(Enumerable.Select<KeyValuePair<string, string>, string>(solrLoggingSerializer.GetAllParameters(queryOptions), (Func<KeyValuePair<string, string>, string>)((KeyValuePair<string, string> p) => $"{p.Key}={p.Value}"))));
        SearchLog.Log.Info(message, null);
        return solrSearchIndex.SolrOperations.Query(text, queryOptions);
      }
      catch (Exception ex)
      {
        if (!(ex is SolrConnectionException) && !(ex is SolrNetException))
        {
          throw;
        }
        string message2 = ex.Message;
        if (ex.Message.StartsWith("<?xml"))
        {
          XmlDocument xmlDocument = new XmlDocument();
          xmlDocument.LoadXml(ex.Message);
          XmlNode xmlNode = xmlDocument.SelectSingleNode("/response/lst[@name='error'][1]/str[@name='msg'][1]");
          XmlNode xmlNode2 = xmlDocument.SelectSingleNode("/response/lst[@name='responseHeader'][1]/lst[@name='params'][1]/str[@name='q'][1]");
          if (xmlNode != null && xmlNode2 != null)
          {
            message2 = $"Solr Error : [\"{xmlNode.InnerText}\"] - Query attempted: [{xmlNode2.InnerText}]";
            SearchLog.Log.Error(message2, null);
            return new SolrQueryResults<Dictionary<string, object>>();
          }
        }
        Log.Error(message2, ex, this);
        return new SolrQueryResults<Dictionary<string, object>>();
      }
    }

    private void AddCultureToFilterQueries(List<CultureExecutionContext> cultureContexts, QueryOptions queryOptions)
    {
      IEnumerable<SolrNotQuery> enumerable = Enumerable.Select<CultureExecutionContext, SolrNotQuery>(Enumerable.Where<CultureExecutionContext>((IEnumerable<CultureExecutionContext>)cultureContexts, (Func<CultureExecutionContext, bool>)((CultureExecutionContext c) => c.PredicateType == CulturePredicateType.Not)), (Func<CultureExecutionContext, SolrNotQuery>)((CultureExecutionContext c) => new SolrNotQuery(CultureToSolrField(c))));
      IEnumerable<SolrQueryByField> enumerable2 = Enumerable.Select<CultureExecutionContext, SolrQueryByField>(Enumerable.Where<CultureExecutionContext>((IEnumerable<CultureExecutionContext>)cultureContexts, (Func<CultureExecutionContext, bool>)((CultureExecutionContext c) => c.PredicateType == CulturePredicateType.Should)), (Func<CultureExecutionContext, SolrQueryByField>)CultureToSolrField);
      IEnumerable<SolrQueryByField> enumerable3 = Enumerable.Select<CultureExecutionContext, SolrQueryByField>(Enumerable.Where<CultureExecutionContext>((IEnumerable<CultureExecutionContext>)cultureContexts, (Func<CultureExecutionContext, bool>)((CultureExecutionContext c) => c.PredicateType == CulturePredicateType.Must)), (Func<CultureExecutionContext, SolrQueryByField>)CultureToSolrField);
      if (Enumerable.Any<SolrNotQuery>(enumerable))
      {
        queryOptions.AddFilterQueries(new SolrMultipleCriteriaQuery(enumerable, "AND"));
      }
      if (Enumerable.Any<SolrQueryByField>(enumerable2))
      {
        queryOptions.AddFilterQueries(new SolrMultipleCriteriaQuery(enumerable2, "OR"));
      }
      if (Enumerable.Any<SolrQueryByField>(enumerable3))
      {
        queryOptions.AddFilterQueries(new SolrMultipleCriteriaQuery(enumerable3, "AND"));
      }
    }

    private static SolrQueryByField CultureToSolrField(CultureExecutionContext c)
    {
      return new SolrQueryByField("_language", c.Culture.TwoLetterISOLanguageName + "*")
      {
        Quoted = false
      };
    }

    private List<CultureExecutionContext> DeterminateCultureContexts(SolrCompositeQuery compositeQuery)
    {
      List<CultureExecutionContext> list = Enumerable.ToList<CultureExecutionContext>(Enumerable.OfType<CultureExecutionContext>((IEnumerable)compositeQuery.ExecutionContexts));
      UpdateFieldNameTranslatorCultureContext(list);
      return list;
    }

    public void UpdateCulture()
    {
      IExecutionContext[] executionContexts = base.Parameters.ExecutionContexts;
      if (executionContexts != null && executionContexts.Length != 0)
      {
        UpdateFieldNameTranslatorCultureContext(Enumerable.Select<IExecutionContext, CultureExecutionContext>(Enumerable.Where<IExecutionContext>((IEnumerable<IExecutionContext>)executionContexts, (Func<IExecutionContext, bool>)((IExecutionContext c) => c is CultureExecutionContext)), (Func<IExecutionContext, CultureExecutionContext>)((IExecutionContext c) => (CultureExecutionContext)c)));
      }
    }

    private void UpdateFieldNameTranslatorCultureContext(IEnumerable<CultureExecutionContext> cultureExecutionContext)
    {
      if (cultureExecutionContext != null)
      {
        CultureExecutionContext cultureExecutionContext2 = Enumerable.FirstOrDefault<CultureExecutionContext>(cultureExecutionContext, (Func<CultureExecutionContext, bool>)((CultureExecutionContext executionContext) => executionContext.PredicateType != CulturePredicateType.Not));
        if (cultureExecutionContext2 != null)
        {
          ((SolrFieldNameTranslator)base.Parameters.FieldNameTranslator).AddCultureContext(cultureExecutionContext2.Culture);
        }
        else
        {
          ((SolrFieldNameTranslator)base.Parameters.FieldNameTranslator).ResetCultureContext();
        }
      }
    }

    private SortOrder[] GetSorting(SolrCompositeQuery compositeQuery)
    {
      return Enumerable.ToArray<SortOrder>(Enumerable.Select<OrderByMethod, SortOrder>(Enumerable.Reverse<OrderByMethod>(Enumerable.Select<QueryMethod, OrderByMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.OrderBy)), (Func<QueryMethod, OrderByMethod>)((QueryMethod m) => (OrderByMethod)m))), (Func<OrderByMethod, SortOrder>)((OrderByMethod sf) => new SortOrder(sf.Field, (sf.SortDirection != 0) ? Order.DESC : Order.ASC))));
    }

    private QueryMethod GetMaxHitsModifierScalarMethod(List<QueryMethod> methods)
    {
      if (methods.Count == 0)
      {
        return null;
      }
      QueryMethod queryMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)methods);
      switch (queryMethod.MethodType)
      {
        case QueryMethodType.Any:
        case QueryMethodType.Count:
        case QueryMethodType.First:
        case QueryMethodType.Last:
        case QueryMethodType.Single:
          return queryMethod;
        default:
          return null;
      }
    }

    private int GetMaxHits(SolrCompositeQuery query, int maxDoc, out int startIdx, out int maxHits)
    {
      List<QueryMethod> obj = (query.Methods != null) ? new List<QueryMethod>(query.Methods) : new List<QueryMethod>();
      obj.Reverse();
      QueryMethod maxHitsModifierScalarMethod = GetMaxHitsModifierScalarMethod(query.Methods);
      startIdx = 0;
      int num = maxDoc;
      int num2 = num;
      int num3 = num;
      int num4 = 0;
      foreach (QueryMethod item in obj)
      {
        switch (item.MethodType)
        {
          case QueryMethodType.Skip:
            {
              int count = ((SkipMethod)item).Count;
              if (count > 0)
              {
                startIdx += count;
              }
              break;
            }
          case QueryMethodType.Take:
            num4 = ((TakeMethod)item).Count;
            if (num4 <= 0)
            {
              num = startIdx++;
            }
            else
            {
              if (num4 > 1 && maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.First)
              {
                num4 = 1;
              }
              if (num4 > 1 && maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.Any)
              {
                num4 = 1;
              }
              if (num4 > 2 && maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.Single)
              {
                num4 = 2;
              }
              num = startIdx + num4 - 1;
              if (num > num2)
              {
                num = num2;
              }
              else if (num2 < num)
              {
                num2 = num;
              }
            }
            break;
        }
      }
      if (num3 == num)
      {
        num4 = -1;
        if (maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.First)
        {
          num4 = 1;
        }
        if (maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.Any)
        {
          num4 = 1;
        }
        if (maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.Single)
        {
          num4 = 2;
        }
        if (num4 >= 0)
        {
          num = startIdx + num4 - 1;
          if (num > num2)
          {
            num = num2;
          }
          else if (num2 < num)
          {
            num2 = num;
          }
        }
      }
      if (num3 == num && startIdx == 0 && maxHitsModifierScalarMethod != null && maxHitsModifierScalarMethod.MethodType == QueryMethodType.Count)
      {
        num = -1;
      }
      maxHits = num4;
      return maxHits;
    }

    private TResult ApplyScalarMethods<TResult, TDocument>(SolrCompositeQuery compositeQuery, SolrSearchResults<TDocument> processedResults, SolrQueryResults<Dictionary<string, object>> results)
    {
      QueryMethod queryMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods);
      object value;
      switch (queryMethod.MethodType)
      {
        case QueryMethodType.All:
          value = true;
          break;
        case QueryMethodType.Any:
          value = processedResults.Any();
          break;
        case QueryMethodType.Count:
          value = ((!LinqToSolrIndex<TItem>.ShouldRunCountOnAllDocuments(compositeQuery)) ? ((object)processedResults.Count()) : ((object)Enumerable.Count<Dictionary<string, object>>((IEnumerable<Dictionary<string, object>>)results)));
          break;
        case QueryMethodType.ElementAt:
          value = ((!((ElementAtMethod)queryMethod).AllowDefaultValue) ? ((object)processedResults.ElementAt(((ElementAtMethod)queryMethod).Index)) : ((object)processedResults.ElementAtOrDefault(((ElementAtMethod)queryMethod).Index)));
          break;
        case QueryMethodType.First:
          value = ((!((FirstMethod)queryMethod).AllowDefaultValue) ? ((object)processedResults.First()) : ((object)processedResults.FirstOrDefault()));
          break;
        case QueryMethodType.Last:
          value = ((!((LastMethod)queryMethod).AllowDefaultValue) ? ((object)processedResults.Last()) : ((object)processedResults.LastOrDefault()));
          break;
        case QueryMethodType.Single:
          value = ((!((SingleMethod)queryMethod).AllowDefaultValue) ? ((object)processedResults.Single()) : ((object)processedResults.SingleOrDefault()));
          break;
        case QueryMethodType.GetResults:
          {
            IEnumerable<SearchHit<TDocument>> searchHits = processedResults.GetSearchHits();
            FacetResults facetResults = this.FormatFacetResults(processedResults.GetFacets(), compositeQuery.FacetQueries);
            value = ReflectionUtility.CreateInstance(typeof(TResult), searchHits, processedResults.NumberFound, facetResults);
            break;
          }
        case QueryMethodType.GetFacets:
          value = this.FormatFacetResults(processedResults.GetFacets(), compositeQuery.FacetQueries);
          break;
        default:
          throw new InvalidOperationException("Invalid query method");
      }
      return (TResult)Convert.ChangeType(value, typeof(TResult));
    }

    private static bool ShouldRunCountOnAllDocuments(SolrCompositeQuery compositeQuery)
    {
      return Enumerable.Any<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)delegate (QueryMethod i)
      {
        if (i.MethodType != QueryMethodType.Take)
        {
          return i.MethodType == QueryMethodType.Skip;
        }
        return true;
      });
    }

    private FacetResults FormatFacetResults(Dictionary<string, ICollection<KeyValuePair<string, int>>> facetResults, List<FacetQuery> facetQueries)
    {
      SolrFieldNameTranslator solrFieldNameTranslator = context.Index.FieldNameTranslator as SolrFieldNameTranslator;
      IDictionary<string, ICollection<KeyValuePair<string, int>>> dictionary = ProcessFacetsPipeline.Run(pipeline, new ProcessFacetsArgs(facetResults, facetQueries, facetQueries, context.Index.Configuration.VirtualFields, solrFieldNameTranslator));
      foreach (FacetQuery facetQuery in facetQueries)
      {
        if (facetQuery.FilterValues != null && Enumerable.Any<object>(facetQuery.FilterValues) && dictionary.ContainsKey(facetQuery.CategoryName))
        {
          ICollection<KeyValuePair<string, int>> source = dictionary[facetQuery.CategoryName];
          dictionary[facetQuery.CategoryName] = Enumerable.ToList<KeyValuePair<string, int>>(Enumerable.Where<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)source, (Func<KeyValuePair<string, int>, bool>)((KeyValuePair<string, int> cv) => Enumerable.Contains<object>(facetQuery.FilterValues, (object)cv.Key))));
        }
      }
      FacetResults facetResults2 = new FacetResults();
      foreach (KeyValuePair<string, ICollection<KeyValuePair<string, int>>> item in dictionary)
      {
        if (solrFieldNameTranslator != null)
        {
          string key = item.Key;
          key = ((!key.Contains(",")) ? solrFieldNameTranslator.StripKnownExtensions(key) : solrFieldNameTranslator.StripKnownExtensions(key.Split(new char[1]
          {
                    ','
          }, StringSplitOptions.RemoveEmptyEntries)));
          IEnumerable<FacetValue> values = Enumerable.Select<KeyValuePair<string, int>, FacetValue>((IEnumerable<KeyValuePair<string, int>>)item.Value, (Func<KeyValuePair<string, int>, FacetValue>)((KeyValuePair<string, int> v) => new FacetValue(v.Key, v.Value)));
          facetResults2.Categories.Add(new FacetCategory(key, values));
        }
      }
      return facetResults2;
    }

    private static SelectMethod GetSelectMethod(SolrCompositeQuery compositeQuery)
    {
      List<SelectMethod> list = Enumerable.ToList<SelectMethod>(Enumerable.Select<QueryMethod, SelectMethod>(Enumerable.Where<QueryMethod>((IEnumerable<QueryMethod>)compositeQuery.Methods, (Func<QueryMethod, bool>)((QueryMethod m) => m.MethodType == QueryMethodType.Select)), (Func<QueryMethod, SelectMethod>)((QueryMethod m) => (SelectMethod)m)));
      if (Enumerable.Count<SelectMethod>((IEnumerable<SelectMethod>)list) != 1)
      {
        return null;
      }
      return list[0];
    }
  }
}