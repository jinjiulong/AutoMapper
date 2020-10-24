using System.Text.RegularExpressions;

namespace AutoMapper
{
    public class ExactMatchNamingConvention : INamingConvention
    {
        public Regex SplittingExpression { get; }
        public string SeparatorCharacter => "";
        public string ReplaceValue(Match match) => match.Value;
    }
}