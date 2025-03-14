using System.Text.RegularExpressions;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core;

public static class RegexPrelude
{
    public static Eff<Option<Match>> regexMatchEff(Regex pattern, string input) =>
        from match in Eff(() => pattern.Match(input))
        select Optional(match).Filter(m => m.Success);

    public static Fin<Option<Match>> regexMatch(Regex pattern, string input) =>
        from match in regexMatchEff(pattern, input).Run()
        select match;

    public static Option<Group> regexGroup(GroupCollection groups, int groupIndex) =>
        Optional(groups[groupIndex]).Filter(g => g.Success);

    public static Option<Group> regexGroup(GroupCollection groups, string groupName) =>
        Optional(groups[groupName]).Filter(g => g.Success);
}
