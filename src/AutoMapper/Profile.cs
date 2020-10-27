using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoMapper.Configuration;
using AutoMapper.Configuration.Conventions;
using AutoMapper.Internal;

namespace AutoMapper
{
    /// <summary>
    ///     Provides a named configuration for maps. Naming conventions become scoped per profile.
    /// </summary>
    public abstract class Profile : IProfileExpressionInternal, IProfileConfiguration
    {
        private readonly List<Action<PropertyMap, IMemberConfigurationExpression>> _allPropertyMapActions =
            new List<Action<PropertyMap, IMemberConfigurationExpression>>();

        private readonly List<Action<TypeMap, IMappingExpression>> _allTypeMapActions =
            new List<Action<TypeMap, IMappingExpression>>();

        private readonly List<string> _globalIgnore = new List<string>();
        private readonly IList<IMemberConfiguration> _memberConfigurations = new List<IMemberConfiguration>();
        private readonly List<ITypeMapConfiguration> _openTypeMapConfigs = new List<ITypeMapConfiguration>();
        private readonly List<MethodInfo> _sourceExtensionMethods = new List<MethodInfo>();
        private readonly List<ITypeMapConfiguration> _typeMapConfigs = new List<ITypeMapConfiguration>();
        private readonly List<ValueTransformerConfiguration> _valueTransformerConfigs = new List<ValueTransformerConfiguration>();
        private bool? _constructorMappingEnabled;

        protected Profile(string profileName) : this() => ProfileName = profileName;

        protected Profile()
        {
            ProfileName = GetType().FullName;
            SourceMemberNamingConvention ??= PascalCaseNamingConvention.Instance;
            DestinationMemberNamingConvention ??= PascalCaseNamingConvention.Instance;
            this.Internal().AddMemberConfiguration()
                .AddMember<NameSplitMember>(n=>
                {
                    n.SourceMemberNamingConvention = SourceMemberNamingConvention;
                    n.DestinationMemberNamingConvention = DestinationMemberNamingConvention;
                })
                .AddName<PrePostfixName>(_ => _.AddStrings(p => p.Prefixes, "Get"));
        }

        protected Profile(string profileName, Action<IProfileExpression> configurationAction)
            : this(profileName)
        {
            configurationAction(this);
        }

        IMemberConfiguration DefaultMemberConfig => _memberConfigurations[0];
        IMemberConfiguration IProfileExpressionInternal.DefaultMemberConfig => DefaultMemberConfig;
        bool? IProfileConfiguration.ConstructorMappingEnabled => _constructorMappingEnabled;
        bool? IProfileConfiguration.EnableNullPropagationForQueryMapping => this.Internal().EnableNullPropagationForQueryMapping;
        IEnumerable<Action<PropertyMap, IMemberConfigurationExpression>> IProfileConfiguration.AllPropertyMapActions
            => _allPropertyMapActions;
        IEnumerable<Action<TypeMap, IMappingExpression>> IProfileConfiguration.AllTypeMapActions => _allTypeMapActions;
        IEnumerable<string> IProfileConfiguration.GlobalIgnores => _globalIgnore;
        IEnumerable<IMemberConfiguration> IProfileConfiguration.MemberConfigurations => _memberConfigurations;
        IEnumerable<MethodInfo> IProfileConfiguration.SourceExtensionMethods => _sourceExtensionMethods;
        IEnumerable<ITypeMapConfiguration> IProfileConfiguration.TypeMapConfigs => _typeMapConfigs;
        IEnumerable<ITypeMapConfiguration> IProfileConfiguration.OpenTypeMapConfigs => _openTypeMapConfigs;
        IEnumerable<ValueTransformerConfiguration> IProfileConfiguration.ValueTransformers => _valueTransformerConfigs;

        public virtual string ProfileName { get; }
        public bool? AllowNullDestinationValues { get; set; }
        public bool? AllowNullCollections { get; set; }
        bool? IProfileExpressionInternal.EnableNullPropagationForQueryMapping { get; set; }
        public Func<PropertyInfo, bool> ShouldMapProperty { get; set; }
        public Func<FieldInfo, bool> ShouldMapField { get; set; }
        public Func<MethodInfo, bool> ShouldMapMethod { get; set; }
        public Func<ConstructorInfo, bool> ShouldUseConstructor { get; set; }
        public virtual INamingConvention SourceMemberNamingConvention { get; set; }
        public virtual INamingConvention DestinationMemberNamingConvention { get; set; }
        public IList<ValueTransformerConfiguration> ValueTransformers => _valueTransformerConfigs;

        public void DisableConstructorMapping() => _constructorMappingEnabled = false;

        void IProfileExpressionInternal.ForAllMaps(Action<TypeMap, IMappingExpression> configuration) => _allTypeMapActions.Add(configuration);

        void IProfileExpressionInternal.ForAllPropertyMaps(Func<PropertyMap, bool> condition, Action<PropertyMap, IMemberConfigurationExpression> configuration) =>
            _allPropertyMapActions.Add((pm, cfg) =>
            {
                if (condition(pm)) configuration(pm, cfg);
            });

        public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => 
            CreateMap<TSource, TDestination>(MemberList.Destination);

        public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList)
        {
            var mappingExp = new MappingExpression<TSource, TDestination>(memberList);
            _typeMapConfigs.Add(mappingExp);
            return mappingExp;
        }

        public IMappingExpression CreateMap(Type sourceType, Type destinationType) => 
            CreateMap(sourceType, destinationType, MemberList.Destination);

        public IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList)
        {
            var map = new MappingExpression(new TypePair(sourceType, destinationType), memberList);
            _typeMapConfigs.Add(map);
            if (sourceType.IsGenericTypeDefinition || destinationType.IsGenericTypeDefinition)
            {
                _openTypeMapConfigs.Add(map);
            }
            return map;
        }

        public void ClearPrefixes() => DefaultMemberConfig.AddName<PrePostfixName>(_ => _.Prefixes.Clear());

        public void RecognizeAlias(string original, string alias) =>
            DefaultMemberConfig.AddName<ReplaceName>(_ => _.AddReplace(original, alias));

        public void ReplaceMemberName(string original, string newValue) =>
            DefaultMemberConfig.AddName<ReplaceName>(_ => _.AddReplace(original, newValue));

        public void RecognizePrefixes(params string[] prefixes) =>
            DefaultMemberConfig.AddName<PrePostfixName>(_ => _.AddStrings(p => p.Prefixes, prefixes));

        public void RecognizePostfixes(params string[] postfixes) =>
            DefaultMemberConfig.AddName<PrePostfixName>(_ => _.AddStrings(p => p.Postfixes, postfixes));

        public void RecognizeDestinationPrefixes(params string[] prefixes) =>
            DefaultMemberConfig.AddName<PrePostfixName>(_ => _.AddStrings(p => p.DestinationPrefixes, prefixes));

        public void RecognizeDestinationPostfixes(params string[] postfixes) =>
            DefaultMemberConfig.AddName<PrePostfixName>(_ => _.AddStrings(p => p.DestinationPostfixes, postfixes));

        public void AddGlobalIgnore(string propertyNameStartingWith) => _globalIgnore.Add(propertyNameStartingWith);

        IMemberConfiguration IProfileExpressionInternal.AddMemberConfiguration()
        {
            var condition = new MemberConfiguration();
            _memberConfigurations.Add(condition);
            return condition;
        }

        public void IncludeSourceExtensionMethods(Type type) =>
            _sourceExtensionMethods.AddRange(type.GetDeclaredMethods().Where(m => m.IsExtensionMethod() && m.GetParameters().Length == 1));
    }
}