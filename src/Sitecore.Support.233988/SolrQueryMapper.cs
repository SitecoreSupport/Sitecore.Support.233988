using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Extensions;
using Sitecore.ContentSearch.Linq.Helpers;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.Pipelines.FormatQueryFieldValue;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Pipelines;
using SolrNet;
using SolrNet.Impl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sitecore.Support.ContentSearch.Linq.Solr
{
  public class SolrQueryMapper : QueryMapper<SolrCompositeQuery>
  {
    protected class SolrQueryMapperState
    {
      public HashSet<QueryMethod> AdditionalQueryMethods
      {
        get;
        set;
      }

      public AbstractSolrQuery FilterQuery
      {
        get;
        set;
      }

      public List<IFieldQueryTranslator> VirtualFieldProcessors
      {
        get;
        set;
      }

      public List<FacetQuery> FacetQueries
      {
        get;
        set;
      }

      public IEnumerable<IExecutionContext> ExecutionContexts
      {
        get;
        set;
      }

      public SolrQueryMapperState(IEnumerable<IExecutionContext> executionContexts)
      {
        AdditionalQueryMethods = new HashSet<QueryMethod>();
        VirtualFieldProcessors = new List<IFieldQueryTranslator>();
        FacetQueries = new List<FacetQuery>();
        ExecutionContexts = executionContexts;
      }
    }

    private readonly IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators;

    public SolrIndexParameters Parameters
    {
      get;
      private set;
    }

    protected FieldNameTranslator FieldNameTranslator
    {
      get;
      set;
    }

    public SolrQueryMapper(SolrIndexParameters parameters)
    {
      if (parameters == null)
      {
        throw new ArgumentNullException("parameters");
      }
      Parameters = parameters;
      base.ValueFormatter = Parameters.ValueFormatter;
      fieldQueryTranslators = Parameters.FieldQueryTranslators;
      FieldNameTranslator = Parameters.FieldNameTranslator;
    }

    public override SolrCompositeQuery MapQuery(IndexQuery query)
    {
      SolrQueryMapperState solrQueryMapperState = new SolrQueryMapperState(Parameters.ExecutionContexts);
      return new SolrCompositeQuery(Visit(query.RootNode, solrQueryMapperState), solrQueryMapperState.FilterQuery, solrQueryMapperState.AdditionalQueryMethods, solrQueryMapperState.VirtualFieldProcessors, solrQueryMapperState.FacetQueries, solrQueryMapperState.ExecutionContexts);
    }

    protected virtual void StripAll(AllNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new AllMethod());
    }

    protected virtual void StripAny(AnyNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new AnyMethod());
    }

    protected virtual void StripCast(CastNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new CastMethod(node.TargetType));
    }

    protected virtual void StripCount(CountNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new CountMethod(node.IsLongCount));
    }

    protected virtual void StripElementAt(ElementAtNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new ElementAtMethod(node.Index, node.AllowDefaultValue));
    }

    protected virtual void StripFirst(FirstNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new FirstMethod(node.AllowDefaultValue));
    }

    protected virtual void StripMin(MinNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new MinMethod(node.AllowDefaultValue));
    }

    protected virtual void StripMax(MaxNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new MaxMethod(node.AllowDefaultValue));
    }

    protected virtual void StripLast(LastNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new LastMethod(node.AllowDefaultValue));
    }

    protected virtual void StripOrderBy(OrderByNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      string text = node.Field.ToLowerInvariant();
      additionalQueryMethods.Add(new OrderByMethod(text.Replace(" ", "_"), node.FieldType, node.SortDirection));
    }

    protected virtual void StripSingle(SingleNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new SingleMethod(node.AllowDefaultValue));
    }

    protected virtual void StripSkip(SkipNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new SkipMethod(node.Count));
    }

    protected virtual void StripTake(TakeNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new TakeMethod(node.Count));
    }

    protected virtual void StripSelect(SelectNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new SelectMethod(node.Lambda, node.FieldNames));
    }

    protected virtual void StripGetResults(GetResultsNode node, HashSet<QueryMethod> additionalQueryMethods)
    {
      additionalQueryMethods.Add(new GetResultsMethod(node.Options));
    }

    protected virtual void StripInContext(InContextNode node, List<IExecutionContext> executionContexts)
    {
      executionContexts.Add(node.ExecutionContext);
    }

    protected virtual void StripGetFacets(GetFacetsNode node, HashSet<QueryMethod> methods)
    {
      methods.Add(new GetFacetsMethod());
    }

    protected virtual void StripFacetOn(FacetOnNode node, SolrQueryMapperState state)
    {
      state.FacetQueries.Add(new FacetQuery(node.Field, new string[1]
      {
            node.Field
      }, node.MinimumNumberOfDocuments, node.FilterValues));
    }

    protected virtual void StripFacetPivotOn(FacetPivotOnNode node, SolrQueryMapperState state)
    {
      state.FacetQueries.Add(new FacetQuery(null, node.Fields, node.MinimumNumberOfDocuments, node.FilterValues));
    }

    protected virtual void StripJoin(JoinNode node, SolrQueryMapperState mappingState)
    {
      mappingState.AdditionalQueryMethods.Add(new JoinMethod(node.GetOuterQueryable(), node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
    }

    protected virtual void StripGroupJoin(GroupJoinNode node, SolrQueryMapperState mappingState)
    {
      mappingState.AdditionalQueryMethods.Add(new GroupJoinMethod(node.GetOuterQueryable(), node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
    }

    protected virtual void StripUnion(UnionNode node, SolrQueryMapperState mappingState)
    {
      mappingState.AdditionalQueryMethods.Add(new UnionMethod(node.GetOuterQueryable(), node.GetInnerQueryable()));
    }

    protected virtual void StripSelectMany(SelectManyNode node, SolrQueryMapperState mappingState)
    {
      mappingState.AdditionalQueryMethods.Add(new SelectManyMethod(node.GetSourceQueryable(), node.CollectionSelectorExpression, node.ResultSelectorExpression));
    }

    protected virtual AbstractSolrQuery Visit(QueryNode node, SolrQueryMapperState state)
    {
      switch (node.NodeType)
      {
        case QueryNodeType.All:
          StripAll((AllNode)node, state.AdditionalQueryMethods);
          return Visit(((AllNode)node).SourceNode, state);
        case QueryNodeType.Any:
          StripAny((AnyNode)node, state.AdditionalQueryMethods);
          return Visit(((AnyNode)node).SourceNode, state);
        case QueryNodeType.Cast:
          StripCast((CastNode)node, state.AdditionalQueryMethods);
          return Visit(((CastNode)node).SourceNode, state);
        case QueryNodeType.Count:
          StripCount((CountNode)node, state.AdditionalQueryMethods);
          return Visit(((CountNode)node).SourceNode, state);
        case QueryNodeType.ElementAt:
          StripElementAt((ElementAtNode)node, state.AdditionalQueryMethods);
          return Visit(((ElementAtNode)node).SourceNode, state);
        case QueryNodeType.First:
          StripFirst((FirstNode)node, state.AdditionalQueryMethods);
          return Visit(((FirstNode)node).SourceNode, state);
        case QueryNodeType.Max:
          StripMax((MaxNode)node, state.AdditionalQueryMethods);
          return Visit(((MaxNode)node).SourceNode, state);
        case QueryNodeType.Min:
          StripMin((MinNode)node, state.AdditionalQueryMethods);
          return Visit(((MinNode)node).SourceNode, state);
        case QueryNodeType.Last:
          StripLast((LastNode)node, state.AdditionalQueryMethods);
          return Visit(((LastNode)node).SourceNode, state);
        case QueryNodeType.OrderBy:
          StripOrderBy((OrderByNode)node, state.AdditionalQueryMethods);
          return Visit(((OrderByNode)node).SourceNode, state);
        case QueryNodeType.Single:
          StripSingle((SingleNode)node, state.AdditionalQueryMethods);
          return Visit(((SingleNode)node).SourceNode, state);
        case QueryNodeType.Skip:
          StripSkip((SkipNode)node, state.AdditionalQueryMethods);
          return Visit(((SkipNode)node).SourceNode, state);
        case QueryNodeType.Take:
          StripTake((TakeNode)node, state.AdditionalQueryMethods);
          return Visit(((TakeNode)node).SourceNode, state);
        case QueryNodeType.Select:
          StripSelect((SelectNode)node, state.AdditionalQueryMethods);
          return Visit(((SelectNode)node).SourceNode, state);
        case QueryNodeType.Filter:
          if (state.FilterQuery == null)
          {
            state.FilterQuery = VisitFilter((FilterNode)node, state);
          }
          else
          {
            AbstractSolrQuery filterQuery = state.FilterQuery;
            state.FilterQuery = (AbstractSolrQuery.op_False(filterQuery) ? filterQuery : (filterQuery & VisitFilter((FilterNode)node, state)));
          }
          return Visit(((FilterNode)node).SourceNode, state);
        case QueryNodeType.GetResults:
          StripGetResults((GetResultsNode)node, state.AdditionalQueryMethods);
          return Visit(((GetResultsNode)node).SourceNode, state);
        case QueryNodeType.InContext:
          {
            List<IExecutionContext> executionContexts = state.ExecutionContexts.ToList();
            StripInContext((InContextNode)node, executionContexts);
            state.ExecutionContexts = executionContexts;
            return Visit(((InContextNode)node).SourceNode, state);
          }
        case QueryNodeType.GetFacets:
          StripGetFacets((GetFacetsNode)node, state.AdditionalQueryMethods);
          return Visit(((GetFacetsNode)node).SourceNode, state);
        case QueryNodeType.FacetOn:
          StripFacetOn((FacetOnNode)node, state);
          return Visit(((FacetOnNode)node).SourceNode, state);
        case QueryNodeType.FacetPivotOn:
          StripFacetPivotOn((FacetPivotOnNode)node, state);
          return Visit(((FacetPivotOnNode)node).SourceNode, state);
        case QueryNodeType.Join:
          StripJoin((JoinNode)node, state);
          return null;
        case QueryNodeType.GroupJoin:
          StripGroupJoin((GroupJoinNode)node, state);
          return null;
        case QueryNodeType.Union:
          {
            UnionNode node2 = (UnionNode)node;
            StripUnion(node2, state);
            return VisitUnion((UnionNode)node, state);
          }
        case QueryNodeType.SelectMany:
          StripSelectMany((SelectManyNode)node, state);
          return null;
        case QueryNodeType.And:
          return VisitAnd((AndNode)node, state);
        case QueryNodeType.Between:
          return VisitBetween((BetweenNode)node, state);
        case QueryNodeType.Contains:
          return VisitContains((ContainsNode)node, state);
        case QueryNodeType.EndsWith:
          return VisitEndsWith((EndsWithNode)node, state);
        case QueryNodeType.Equal:
          return VisitEqual((EqualNode)node, state);
        case QueryNodeType.LessThanOrEqual:
          return VisitLessThanOrEqual((LessThanOrEqualNode)node, state);
        case QueryNodeType.LessThan:
          return VisitLessThan((LessThanNode)node, state);
        case QueryNodeType.GreaterThanOrEqual:
          return VisitGreaterThanOrEqual((GreaterThanOrEqualNode)node, state);
        case QueryNodeType.GreaterThan:
          return VisitGreaterThan((GreaterThanNode)node, state);
        case QueryNodeType.MatchAll:
          return VisitMatchAll((MatchAllNode)node, state);
        case QueryNodeType.MatchNone:
          return VisitMatchNone((MatchNoneNode)node, state);
        case QueryNodeType.Not:
          return VisitNot((NotNode)node, state);
        case QueryNodeType.Or:
          return VisitOr((OrNode)node, state);
        case QueryNodeType.StartsWith:
          return VisitStartsWith((StartsWithNode)node, state);
        case QueryNodeType.Where:
          return VisitWhere((WhereNode)node, state);
        case QueryNodeType.Field:
          return VisitField((FieldNode)node, state);
        case QueryNodeType.Matches:
          return VisitMatches((MatchesNode)node, state);
        case QueryNodeType.WildcardMatch:
          return VisitWildcardMatch((WildcardMatchNode)node, state);
        case QueryNodeType.Like:
          return VisitLike((LikeNode)node, state);
        case QueryNodeType.SelfJoin:
          return VisitSelfJoin((SelfJoinNode)node, state);
        default:
          throw new NotSupportedException($"Unknown query node type: '{node.NodeType}'");
      }
    }

    protected virtual AbstractSolrQuery VisitFilter(FilterNode node, SolrQueryMapperState state)
    {
      SolrQueryMapperState state2 = new SolrQueryMapperState(Parameters.ExecutionContexts);
      return Visit(node.PredicateNode, state2);
    }

    protected virtual AbstractSolrQuery VisitField(FieldNode node, SolrQueryMapperState state)
    {
      if (node.FieldType != typeof(bool))
      {
        throw new NotSupportedException($"The query node type '{node.NodeType}' is not supported in this context.");
      }
      string text = node.FieldKey.ToLowerInvariant();
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = text,
        FieldValue = true.ToString(),
        Translator = FieldNameTranslator,
        SuppressMappingRule = true
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      return new SolrQueryByField(text.Replace(" ", "_"), formatQueryFieldValueArgs.FieldValue);
    }

    protected virtual AbstractSolrQuery VisitUnion(UnionNode node, SolrQueryMapperState state)
    {
      return Visit(node.OuterQuery, state) + Visit(node.InnerQuery, state);
    }

    protected virtual AbstractSolrQuery VisitAnd(AndNode node, SolrQueryMapperState state)
    {
      AbstractSolrQuery abstractSolrQuery = Visit(node.LeftNode, state);
      AbstractSolrQuery b = Visit(node.RightNode, state);
      AbstractSolrQuery abstractSolrQuery2 = abstractSolrQuery;
      if (!AbstractSolrQuery.op_False(abstractSolrQuery2))
      {
        return abstractSolrQuery2 & b;
      }
      return abstractSolrQuery2;
    }

    protected virtual AbstractSolrQuery VisitBetween(BetweenNode node, SolrQueryMapperState state)
    {
      bool flag = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Lower;
      bool flag2 = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Upper;
      string text = node.Field.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(node.From, text), text);
      object obj2 = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(node.To, text), text);
      if (obj is string && ((string)obj).Contains(" "))
      {
        obj = "\"" + (string)obj + "\"";
      }
      if (obj2 is string && ((string)obj2).Contains(" "))
      {
        obj2 = "\"" + (string)obj2 + "\"";
      }
      AbstractSolrQuery abstractSolrQuery = (AbstractSolrQuery)ReflectionUtility.CreateInstance(typeof(SolrQueryByRange<>).MakeGenericType(obj.GetType()), text.Replace(" ", "_"), obj, obj2, flag, flag2);
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitContains(ContainsNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.Contains, state, out result))
      {
        return result;
      }
      return VisitContains(fieldNode.FieldKey, valueNode.Value, node.Boost);
    }

    protected virtual AbstractSolrQuery VisitContains(string field, object value, float boost)
    {
      field = field.ToLowerInvariant();
      base.ValueFormatter.FormatValueForIndexStorage(value, field);
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = field,
        FieldValue = value.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(field.Replace(" ", "_"), "*" + formatQueryFieldValueArgs.FieldValue + "*")
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitEndsWith(EndsWithNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      string text = fieldNode.FieldKey.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(valueNode.Value, text);
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = text,
        FieldValue = obj.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(text.Replace(" ", "_"), "*" + formatQueryFieldValueArgs.FieldValue)
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitEqual(EqualNode node, SolrQueryMapperState state)
    {
      if (node.LeftNode is ConstantNode && node.RightNode is ConstantNode)
      {
        if (((ConstantNode)node.LeftNode).Value.Equals(((ConstantNode)node.RightNode).Value))
        {
          return SolrQuery.All;
        }
        return new SolrNotQuery(SolrQuery.All);
      }
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode constantNode = (!(fieldNode.FieldType != typeof(string))) ? QueryHelper.GetValueNode<object>((BinaryNode)node) : QueryHelper.GetValueNode(node, fieldNode.FieldType);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, constantNode, node.Boost, ComparisonType.Equal, state, out result))
      {
        return result;
      }
      string fieldKey = fieldNode.FieldKey;
      object value = constantNode.Value;
      float boost = node.Boost;
      return VisitEqual(fieldKey, value, boost);
    }

    protected virtual AbstractSolrQuery VisitEqual(string field, object fieldValue, float boost)
    {
      object value = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(fieldValue, field), field);
      field = field.ToLowerInvariant().Replace(" ", "_");
      string text = value.ToStringOrEmpty();
      AbstractSearchFieldConfiguration abstractSearchFieldConfiguration = (Parameters.FieldMap != null) ? Parameters.FieldMap.GetFieldConfiguration(field) : null;
      if (abstractSearchFieldConfiguration != null)
      {
        if (text == string.Empty && abstractSearchFieldConfiguration.EmptyString != null)
        {
          text = abstractSearchFieldConfiguration.EmptyString;
        }
        else if (text == null && abstractSearchFieldConfiguration.NullValue != null)
        {
          text = abstractSearchFieldConfiguration.NullValue;
        }
      }
      bool flag = !text.EndsWith("Z]");
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = field,
        FieldValue = text,
        IsQuoted = flag,
        Translator = FieldNameTranslator,
        SuppressIsQuotedRule = !flag
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(field, formatQueryFieldValueArgs.FieldValue)
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitLessThanOrEqual(LessThanOrEqualNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.LessThanOrEqual, state, out result))
      {
        return result;
      }
      string fieldKey = fieldNode.FieldKey;
      object value = valueNode.Value;
      float boost = node.Boost;
      return VisitLessThanOrEqual(fieldKey, value, boost);
    }

    protected virtual AbstractSolrQuery VisitLessThanOrEqual(string field, object value, float boost)
    {
      object obj = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(value, field), field);
      field = field.ToLowerInvariant().Replace(" ", "_");
      Type nullableType = GetNullableType(obj.GetType());
      AbstractSolrQuery abstractSolrQuery = (AbstractSolrQuery)ReflectionUtility.CreateInstance(typeof(SolrQueryByRange<>).MakeGenericType(nullableType), field, null, obj, true, true);
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitLessThan(LessThanNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.LessThan, state, out result))
      {
        return result;
      }
      string fieldKey = fieldNode.FieldKey;
      object value = valueNode.Value;
      float boost = node.Boost;
      return VisitLessThan(fieldKey, value, boost);
    }

    protected virtual AbstractSolrQuery VisitLessThan(string field, object value, float boost)
    {
      object obj = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(value, field), field);
      field = field.ToLowerInvariant().Replace(" ", "_");
      Type nullableType = GetNullableType(obj.GetType());
      AbstractSolrQuery abstractSolrQuery = (AbstractSolrQuery)ReflectionUtility.CreateInstance(typeof(SolrQueryByRange<>).MakeGenericType(nullableType), field, null, obj, true, false);
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitGreaterThanOrEqual(GreaterThanOrEqualNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.GreaterThanOrEqual, state, out result))
      {
        return result;
      }
      string fieldKey = fieldNode.FieldKey;
      object value = valueNode.Value;
      float boost = node.Boost;
      return VisitGreaterThanOrEqual(fieldKey, value, boost);
    }

    protected virtual AbstractSolrQuery VisitGreaterThanOrEqual(string field, object value, float boost)
    {
      object obj = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(value, field), field);
      field = field.ToLowerInvariant().Replace(" ", "_");
      Type nullableType = GetNullableType(obj.GetType());
      AbstractSolrQuery abstractSolrQuery = (AbstractSolrQuery)ReflectionUtility.CreateInstance(typeof(SolrQueryByRange<>).MakeGenericType(nullableType), field, obj, null, true, true);
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitGreaterThan(GreaterThanNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.GreaterThan, state, out result))
      {
        return result;
      }
      string fieldKey = fieldNode.FieldKey;
      object value = valueNode.Value;
      float boost = node.Boost;
      return VisitGreaterThan(fieldKey, value, boost);
    }

    protected virtual AbstractSolrQuery VisitGreaterThan(string field, object value, float boost)
    {
      object obj = base.ValueFormatter.FormatValueForIndexStorage(ConvertDateTimeToUtc(value, field), field);
      field = field.ToLowerInvariant().Replace(" ", "_");
      Type nullableType = GetNullableType(obj.GetType());
      AbstractSolrQuery abstractSolrQuery = (AbstractSolrQuery)ReflectionUtility.CreateInstance(typeof(SolrQueryByRange<>).MakeGenericType(nullableType), field, obj, null, false, true);
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitMatchAll(MatchAllNode node, SolrQueryMapperState state)
    {
      return SolrQuery.All;
    }

    protected virtual AbstractSolrQuery VisitMatchNone(MatchNoneNode node, SolrQueryMapperState state)
    {
      return new SolrNotQuery(SolrQuery.All);
    }

    protected virtual AbstractSolrQuery VisitNot(NotNode node, SolrQueryMapperState state)
    {
      AbstractSolrQuery abstractSolrQuery = Visit(node.Operand, state);
      SolrQueryByField solrQueryByField = abstractSolrQuery as SolrQueryByField;
      if (solrQueryByField != null && string.IsNullOrEmpty(solrQueryByField.FieldValue))
      {
        return new SolrMultipleCriteriaQuery(new ISolrQuery[2]
        {
                new SolrNotQuery(abstractSolrQuery),
                new SolrHasValueQuery(solrQueryByField.FieldName)
        });
      }
      SolrNotQuery solrNotQuery = abstractSolrQuery as SolrNotQuery;
      if (solrNotQuery != null)
      {
        return (AbstractSolrQuery)solrNotQuery.Query;
      }
      return new SolrMultipleCriteriaQuery(new ISolrQuery[2]
      {
            new SolrNotQuery(abstractSolrQuery),
            SolrQuery.All
      });
    }

    protected virtual AbstractSolrQuery VisitOr(OrNode node, SolrQueryMapperState state)
    {
      AbstractSolrQuery abstractSolrQuery;
      if (node.LeftNode.NodeType == QueryNodeType.Equal && node.RightNode.NodeType == QueryNodeType.Equal && ((EqualNode)node.LeftNode).RightNode.NodeType == QueryNodeType.Constant && ((EqualNode)node.RightNode).RightNode.NodeType == QueryNodeType.Constant)
      {
        object obj = ((ConstantNode)((EqualNode)node.LeftNode).RightNode).Value;
        string fieldKey = ((FieldNode)((EqualNode)node.LeftNode).LeftNode).FieldKey;
        object obj2 = ((ConstantNode)((EqualNode)node.RightNode).RightNode).Value;
        if (obj is string && (string)obj == string.Empty && obj2 == null)
        {
          AbstractSearchFieldConfiguration fieldConfiguration = Parameters.FieldMap.GetFieldConfiguration(fieldKey);
          if (fieldConfiguration != null && (string)obj == string.Empty && fieldConfiguration.EmptyString != null)
          {
            obj = fieldConfiguration.EmptyString;
          }
          if (fieldConfiguration != null && obj2 == null && fieldConfiguration.NullValue != null)
          {
            obj2 = fieldConfiguration.NullValue;
          }
          if (obj2 != null && obj != null)
          {
            abstractSolrQuery = new SolrQuery(fieldKey + ":" + obj);
            if (!AbstractSolrQuery.op_True(abstractSolrQuery))
            {
              return abstractSolrQuery | (AbstractSolrQuery)new SolrQuery(fieldKey + ":" + obj2);
            }
            return abstractSolrQuery;
          }
          return new SolrNotQuery(new SolrHasValueQuery(fieldKey));
        }
      }
      AbstractSolrQuery abstractSolrQuery2 = Visit(node.LeftNode, state);
      AbstractSolrQuery abstractSolrQuery3 = Visit(node.RightNode, state);
      SolrMultipleCriteriaQuery solrMultipleCriteriaQuery = abstractSolrQuery2 as SolrMultipleCriteriaQuery;
      if (solrMultipleCriteriaQuery != null && solrMultipleCriteriaQuery.Oper == "OR")
      {
        ISolrQuery[] array = ((SolrMultipleCriteriaQuery)abstractSolrQuery2).Queries.ToArray();
        Array.Resize(ref array, array.Length + 1);
        array[array.Length - 1] = abstractSolrQuery3;
        return new SolrMultipleCriteriaQuery(array, "OR");
      }
      abstractSolrQuery = abstractSolrQuery2;
      return abstractSolrQuery ? abstractSolrQuery : (abstractSolrQuery | abstractSolrQuery3);
    }

    protected virtual AbstractSolrQuery VisitStartsWith(StartsWithNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.StartsWith, state, out result))
      {
        return result;
      }
      return VisitStartsWith(fieldNode.FieldKey, valueNode.Value, node.Boost);
    }

    protected virtual AbstractSolrQuery VisitStartsWith(string field, object value, float boost)
    {
      field = field.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(value, field);
      obj = new Regex("^\\/", RegexOptions.Compiled).Replace(obj.ToString(), "\\/");
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = field,
        FieldValue = obj.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator,
        ExcludeEscapeCharacters = new Collection<string>
            {
                "\\",
                "/"
            },
        SuppressIsQuotedRule = (fieldQueryTranslators.GetTranslator(field.ToLowerInvariant()) != null)
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(field.Replace(" ", "_"), formatQueryFieldValueArgs.FieldValue + "*")
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected virtual AbstractSolrQuery VisitWhere(WhereNode node, SolrQueryMapperState state)
    {
      AbstractSolrQuery abstractSolrQuery = Visit(node.PredicateNode, state);
      AbstractSolrQuery abstractSolrQuery2 = Visit(node.SourceNode, state);
      if (abstractSolrQuery == SolrQuery.All && abstractSolrQuery2 == SolrQuery.All)
      {
        return abstractSolrQuery;
      }
      if (abstractSolrQuery == SolrQuery.All || abstractSolrQuery2 == SolrQuery.All)
      {
        if (abstractSolrQuery != SolrQuery.All)
        {
          return abstractSolrQuery;
        }
        if (abstractSolrQuery2 != SolrQuery.All)
        {
          return abstractSolrQuery2;
        }
      }
      AbstractSolrQuery abstractSolrQuery3 = abstractSolrQuery;
      if (!AbstractSolrQuery.op_False(abstractSolrQuery3))
      {
        return abstractSolrQuery3 & abstractSolrQuery2;
      }
      return abstractSolrQuery3;
    }

    protected virtual AbstractSolrQuery VisitMatches(MatchesNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByFieldRegex(fieldNode.FieldKey.Replace(" ", "_").ToLowerInvariant(), valueNode.Value.ToStringOrEmpty());
      if (node.RegexOptions != null)
      {
        throw new NotSupportedException($"Unsupported node: {node.GetType().FullName} - Specifying RegexOptions is not supported for Solr.");
      }
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    protected AbstractSolrQuery VisitWildcardMatch(WildcardMatchNode node, SolrQueryMapperState mappingState)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      string text = fieldNode.FieldKey.ToLowerInvariant();
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = text,
        FieldValue = valueNode.Value.ToStringOrEmpty(),
        IsQuoted = false,
        Translator = FieldNameTranslator,
        SuppressMappingRule = true
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(text.Replace(" ", "_"), formatQueryFieldValueArgs.FieldValue)
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    protected AbstractSolrQuery VisitLike(LikeNode node, SolrQueryMapperState mappingState)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      string text = fieldNode.FieldKey.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(valueNode.Value, text);
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = text,
        FieldValue = obj.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator,
        EscapeCharacter = "\\",
        IncludeExistingCharacter = new bool?(true)
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      string fieldValue = $"{formatQueryFieldValueArgs.FieldValue}~{node.MinimumSimilarity.ToString(CultureInfo.InvariantCulture)}";
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(text.Replace(" ", "_"), fieldValue)
      {
        Quoted = formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    private AbstractSolrQuery VisitSelfJoin(SelfJoinNode node, SolrQueryMapperState mappingState)
    {
      AbstractSolrQuery q = Visit(node.OuterQuery, mappingState);
      AbstractSolrQuery q2 = Visit(node.InnerQuery, new SolrQueryMapperState(new IExecutionContext[0]));
      ISolrQuerySerializer querySerializer = SolrQuerySerializerUtility.GetQuerySerializer();
      string arg = querySerializer.Serialize(q);
      string arg2 = querySerializer.Serialize(q2);
      SolrQuery result = new SolrQuery(string.Format("{{!join from={0} to={1}}}{2}", node.OuterKey.ToLowerInvariant().Replace(" ", "_"), node.InnerKey.ToLowerInvariant().Replace(" ", "_"), arg2));
      mappingState.FilterQuery = new SolrQuery($"{arg}");
      return result;
    }

    protected virtual bool ProcessAsVirtualField(FieldNode fieldNode, ConstantNode valueNode, float boost, ComparisonType comparison, SolrQueryMapperState state, out AbstractSolrQuery query)
    {
      query = null;
      if (fieldQueryTranslators == null)
      {
        return false;
      }
      IFieldQueryTranslator translator = fieldQueryTranslators.GetTranslator(fieldNode.FieldKey.ToLowerInvariant());
      if (translator == null)
      {
        return false;
      }
      object fieldValue = base.ValueFormatter.FormatValueForIndexStorage(valueNode.Value, fieldNode.FieldKey);
      TranslatedFieldQuery translatedFieldQuery = translator.TranslateFieldQuery(fieldNode.FieldKey, fieldValue, comparison, FieldNameTranslator);
      if (translatedFieldQuery == null)
      {
        return false;
      }
      List<AbstractSolrQuery> list = new List<AbstractSolrQuery>();
      if (translatedFieldQuery.FieldComparisons != null)
      {
        foreach (Tuple<string, object, ComparisonType> fieldComparison in translatedFieldQuery.FieldComparisons)
        {
          string indexFieldName = FieldNameTranslator.GetIndexFieldName(fieldComparison.Item1);
          switch (fieldComparison.Item3)
          {
            case ComparisonType.Equal:
              list.Add(VisitEqual(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.LessThan:
              list.Add(VisitLessThan(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.LessThanOrEqual:
              list.Add(VisitLessThanOrEqual(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.GreaterThan:
              list.Add(VisitGreaterThan(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.GreaterThanOrEqual:
              list.Add(VisitGreaterThanOrEqual(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.StartsWith:
              list.Add(VisitStartsWith(indexFieldName, fieldComparison.Item2, boost));
              break;
            case ComparisonType.Contains:
              list.Add(VisitContains(indexFieldName, fieldComparison.Item2, boost));
              break;
            default:
              throw new InvalidOperationException("Unsupported comparison type: " + fieldComparison.Item3);
          }
        }
        foreach (AbstractSolrQuery item in list)
        {
          if (query == null)
          {
            query = item;
          }
          else
          {
            AbstractSolrQuery abstractSolrQuery = query;
            query = (AbstractSolrQuery.op_False(abstractSolrQuery) ? abstractSolrQuery : (abstractSolrQuery & item));
          }
        }
      }
      if (translatedFieldQuery.QueryMethods != null)
      {
        foreach (QueryMethod queryMethod in translatedFieldQuery.QueryMethods)
        {
          state.AdditionalQueryMethods.Add(queryMethod);
        }
      }
      state.VirtualFieldProcessors.Add(translator);
      return true;
    }

    private object ConvertDateTimeToUtc(object objectToConvert, string fieldName)
    {
      if (objectToConvert != null && objectToConvert is DateTime)
      {
        DateTime serverTime = (DateTime)objectToConvert;
        if (serverTime.Kind != DateTimeKind.Utc && Parameters.ConvertQueryDatesToUtc)
        {
          return ContentSearchManager.Locator.GetInstance<IDateTimeConverter>().ToUniversalTime(serverTime);
        }
        return objectToConvert;
      }
      return objectToConvert;
    }

    private Type GetNullableType(Type type)
    {
      if (!type.IsValueType)
      {
        return type;
      }
      return typeof(Nullable<>).MakeGenericType(type);
    }

    protected virtual void RunFormatQueryFieldValuePipeline(FormatQueryFieldValueArgs args)
    {
      string pipelineName = "contentSearch.formatQueryFieldValue";
      if (CorePipelineFactory.GetPipeline(pipelineName, string.Empty) != null)
      {
        CorePipeline.Run(pipelineName, args);
      }
    }
  }
}