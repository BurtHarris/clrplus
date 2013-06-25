//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

#define NEW_MODEL

namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using CSScriptLibrary;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheetV3.Mapping;

    public class Pivots : AbstractDictionary<string, Pivots.Pivot> {
          private class ExpressionTemplate {
              internal static ExpressionTemplate CSharp = new ExpressionTemplate {
                  And = " && ",
                  Or = " || ",
                  AndNot = " && !",
                  OrNot = " || !",
                  Not = "!",
                  Container = "( {0} )",
                  Comparison = (projectName, choice, pivot) => "{0} == {0}.@{1}".format(pivot.Name, choice),
              };

              internal static ExpressionTemplate MSBuild = new ExpressionTemplate {
                  And = " And ",
                  Or = " Or ",
                  AndNot = " And !",
                  OrNot = " Or !",
                  Not = "!",
                  Container = "( {0} )",

                  Comparison = (projectName, choice, pivot) => {
                      // mark that choice as used.
                      pivot.UsedChoices.Add(choice);

                      if (!pivot.IsBuiltIn) {
                          return "'$({1}-{0}.ToLower())' == '{2}'".format(projectName.MakeSafeFileName().Replace(".", "_"), pivot.Name, choice.ToLower());
                      }

                      if (pivot.Conditions.ContainsKey(choice)) {
                          return pivot.Conditions[choice];
                      }

                      return "'$({0}.ToLower())' == '{1}'".format(pivot.Key, choice.ToLower());
                  }
              };

              internal static ExpressionTemplate Label = new ExpressionTemplate {
                  And = " and ",
                  Or = " or ",
                  AndNot = " and not ",
                  OrNot = " or not ",
                  Not = " not ",
                  Container = "({0})",
                  Comparison = (projectName, choice, pivot) => {
                      // mark that choice as used.
                      pivot.UsedChoices.Add(choice);
                      return choice;
                  }  
              };

              internal static ExpressionTemplate Path = new ExpressionTemplate {
                  And = "\\",
                  Or = "+",
                  AndNot = "\\!",
                  OrNot = "+!",
                  Not = "!",
                  Container = "({0})",
                  Comparison = (projectName, choice, pivot) => {
                      // mark that choice as used.
                      pivot.UsedChoices.Add(choice);
                      return choice;
                  } 
              };

            internal string And;
            internal string Or;
            internal string AndNot;
            internal string OrNot;
            internal string Not;
            internal string Container;
            internal GenerateComparison Comparison;

            internal delegate string GenerateComparison(string projectName, string item, Pivot pivot);
        }


        private AnswerCache<bool> _compareCache = new AnswerCache<bool>();
        private AnswerCache<bool[]> _expressionArrayCache = new AnswerCache<bool[]>();
        private AnswerCache<string> _expressionCache = new AnswerCache<string>();


        private AnswerCache<Pivot[]> _pivotsUsedCache = new AnswerCache<Pivot[]>();

        private static readonly Regex WordRx = new Regex(@"^\w*$");

        private static readonly Regex ExpressionRx = new Regex(
            @"^\s*
    (\(              # Match an opening parenthesis. (with a potential !
      (?>             # Then either match (possessively):
       [^()]+         #  any characters except parentheses
      |               # or
       \( (?<Depth>)  #  an opening paren (and increase the parens counter)
      |               # or
       \) (?<-Depth>) #  a closing paren (and decrease the parens counter).
      )*              # Repeat as needed.
     (?(Depth)(?!))   # Assert that the parens counter is at zero.
     \)               # Then match a closing parenthesis.
     |\s*|
      &+
      |
      \|+
      |
      \++
      |
      /+
      |
      \\+|
      |
      \,+
      |
      !+
      |
      \w+
      |
      .*
      )*\s*$", RegexOptions.IgnorePatternWhitespace);

        // private readonly HashSet<string> _canonicalExpressions = new HashSet<string>();
        private readonly Dictionary<string,List<string>> _canonicalExpressions = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, Pivot> _pivots = new Dictionary<string, Pivot>();

        internal Pivots(View configurationsView) {
            var names = configurationsView.GetChildPropertyNames();
            foreach (var n in names) {
                var eachPivot = configurationsView.GetProperty(n);
                var choices = eachPivot.HasChild("choices") ? eachPivot.GetProperty("choices").Values.Distinct().ToArray() : new string[0];
                if (choices.Length == 0) {
                    continue;
                }

                var piv = new Pivot {
                    Name = n,
                    Key = eachPivot.HasChild("key") ? eachPivot.GetProperty("key").Value : null,
                    Description = eachPivot.HasChild("description") ? eachPivot.GetProperty("description").Value : n,
                    EnumCode = @"[Flags] 
                    enum {0} : ulong  {{
                        None  = 0
                        {1}
                    }};".format(n, choices.Select((choice, bit) => "   ,@{0} = {1}\r\n".format(choice, (1 << bit))).Aggregate((current, each) => current + each))
                };

                foreach (var choice in choices) {
                    var ch = eachPivot.GetProperty(choice);
                    if(!ch.HasChildren) {
                        piv.Descriptions.Add(choice, choice);
                        piv.Choices.Add(choice, choice.ToLower().SingleItemAsEnumerable());
                    } else {
                        if (ch.HasChild("condition")) {
                            piv.IsBuiltIn = true; // if one of these has a condtion, then they all better.
                            piv.Conditions.Add(choice, ch.GetProperty("condition").Value);   
                        };
                        piv.Descriptions.Add(choice, ch.HasChild("description") ? ch.GetProperty("description").Value : choice);
                        piv.Choices.Add(choice, ch.HasChild("aliases") ? ch.GetProperty("aliases").Values.Select(each => each.ToLower()) : choice.ToLower().SingleItemAsEnumerable());
                    }
                }
                _pivots.Add(n, piv);
            }
        }

        public override Pivot this[string key] {
            get {
                return _pivots[key];
            }
            set {
                throw new NotImplementedException();
            }
        }

        public override ICollection<string> Keys {
            get {
                return _pivots.Keys;
            }
        }

        public override bool IsReadOnly {
            get {
                return true;
            }
        }

        public string NormalizeExpression(string expression) {
            if (string.IsNullOrEmpty(expression)) {
                return expression;
            }
            /*
            if (_canonicalExpressions.Keys.Contains(expression)) {
                return expression;
            }*/
            foreach (var i in _canonicalExpressions.Keys) {
                if (_canonicalExpressions[i].Contains(expression)) {
                    return i;
                }
            }

            foreach (var i in _canonicalExpressions.Keys) {
                if (CompareExpressions(i, expression)) {
                    _canonicalExpressions[i].Add(expression);
                    return i;
                }
            }
            _canonicalExpressions.Add(expression,new List<string>{expression});
            return expression;
        }

        public string GetExpressionFilepath(string packageName, string expression) {
            if (string.IsNullOrEmpty(expression)) {
                return string.Empty;
            }
            
            return GenerateExpression(packageName, NormalizeExpression(expression), ExpressionTemplate.Path, new List<Pivot>());
        }

        public string GetExpressionLabel(string expression) {
            if(string.IsNullOrEmpty(expression)) {
                return string.Empty;
            }

            return GenerateExpression("", NormalizeExpression(expression), ExpressionTemplate.Label,new List<Pivot>());
        }

        
        public string GetMSBuildCondition(string projectName, string expression) {
            return GenerateExpression(projectName, NormalizeExpression(expression), ExpressionTemplate.MSBuild,new List<Pivot>());
        }

        internal bool[] GetExpressionArray(string rightExpression, string rightexpress,Pivot[] pivotsUsed) {

            return _expressionArrayCache.GetCachedAnswer(() => {

#if NEW_MODEL

                //var pivotKeys = pivotsUsed.Select(each => each.Name).ToArray();
                var param = pivotsUsed.Select((each, i) => "{0} {0},".format(each.Name)).Aggregate((current, each) => current + each).Trim(',');
                var enums = pivotsUsed.Select(each => each.EnumCode).Aggregate((current, each) => "{0}\r\n{1}".format(current, each));
                var fors = pivotsUsed.Select((each, i) => "foreach (var _{1} in Enum.GetValues(typeof ({0})).Cast<{0}>()) {{\r\n".format(each.Name, i)).ToArray();
#else 
                var param = _pivots.Keys.Select((each, i) => "{0} {0},".format(each)).Aggregate((current, each) => current + each).Trim(',');

                var used = new List<Pivot>();
                rightexpress = GenerateExpression("", rightExpression, ExpressionTemplate.CSharp, used);
                var enums = _pivots.Values.Select(each => each.EnumCode).Aggregate((current, each) => "{0}\r\n{1}".format(current, each));
                var fors = _pivots.Keys.Select((each, i) => "foreach (var _{1} in Enum.GetValues(typeof ({0})).Cast<{0}>()) {{\r\n".format(each, i)).ToArray();
#endif

                var call = fors.Select((each, i) => "_{0},".format(i)).Aggregate((current, each) => current + each).Trim(',');
                var fn = "result[--n] = ProcessRight({0});".format(call);
                var close = fors.Select(each => "}\r\n").Aggregate((each, current) => current + each);
                var loops = fors.Aggregate((current, each) => current + each) + fn + close;

                var script = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class Script
    {{
        // takes a variable list of all the values from all the pivots
        internal bool ProcessRight({0}) {{
            return {1};
        }}

        public bool[] GetAll(int n) {{
            var result = new bool[n];
            {2}
            return result;
        }}
    }}

    //enums
    {3}
".format(param, rightexpress.Is() ? rightexpress : "true", loops, enums);
               
#if NEW_MODEL
                var c = pivotsUsed.Select(each => each.Choices.Count + 1).Aggregate(1, (current, i) => current*i);
#else
                var c = _pivots.Values.Select(each => each.Choices.Count + 1).Aggregate(1, (current, i) => current*i);
#endif

                 // Event<Verbose>.Raise("Script", "\r\n======\r\n{0}\r\n======", script);

                try {
                    Event<Verbose>.Raise("Pivots", "Pivot Count: {0}".format(c));
                    dynamic s = CSScript.Evaluator.LoadCode(script);
                    bool[] result = s.GetAll(c + 10);
                    return result;
                } catch {
                    // GS01 : This f'er is throwing the first time. And only the first time.
                    // No idea WHY this happens, or more importantly, why it doesn't throw ever the second time.
                    // it's got something going haywire with loading AsyncExtensions Class 
                    // (not that we actually use that here)
                    // *sigh*
                    Event<Verbose>.Raise("GS01", "ODD BUG STILL HERE");
                    dynamic s = CSScript.Evaluator.LoadCode(script);
                    bool[] result = s.GetAll(c + 10);
                    return result;
                }
                //Event<Verbose>.Raise("Pivots.CompareExpressions", "[{0}] vs [{1}] == {2}", leftExpression, rightExpression, result);
            },  rightExpression);
        }

        internal bool CompareExpressions(string leftExpression, string rightExpression) {
            if(leftExpression.IndexOf("$(") > -1 || rightExpression.IndexOf("$(") > -1) {
                return false;
            }

            if (leftExpression.Equals(rightExpression)) {
                return true;
            }

#if NEW_MODEL
            var rightPivotsUsed = new List<Pivot>();
            var rightexpress = GenerateExpression("", rightExpression, ExpressionTemplate.CSharp, rightPivotsUsed);

            var leftPivotsUsed = new List<Pivot>();
            var leftexpress = GenerateExpression("", leftExpression, ExpressionTemplate.CSharp, leftPivotsUsed);

            if (rightPivotsUsed.Count == leftPivotsUsed.Count && rightPivotsUsed.ContainsAll(leftPivotsUsed) ) {
                var used = rightPivotsUsed.ToArray();
                var v1 = GetExpressionArray(leftExpression, leftexpress, used);
                var v2 = GetExpressionArray(rightExpression, rightexpress, used);
                return v1.SequenceEqual(v2);
            }
             return false;
#else 
            var v1 = GetExpressionArray(leftExpression,null,null);
            var v2 = GetExpressionArray(rightExpression, null, null);
            return v1.SequenceEqual(v2);
#endif
        }

        private string GenerateExpression(string projectName, string expression,ExpressionTemplate template, List<Pivot> pivotsUsed  ) {
            

            var theresult = _expressionCache.GetCachedAnswer(() => {
                if (expression.IndexOf("$(") > -1) {
                    // skip the whole parsing, we know this is a MSBuild expression
                    return expression;
                }

                var result = new StringBuilder();
                var rxResult = ExpressionRx.Match(expression);
                if (rxResult.Success) {
                    var state = ExpressionState.None;

                    foreach (var item in rxResult.Groups[1].Captures.Cast<Capture>().Select(each => each.Value.Trim()).Where(each => !string.IsNullOrEmpty(each))) {
                        switch (item[0]) {
                            case '!':
                                if (state > ExpressionState.HasOperator) {
                                    throw new ClrPlusException("Invalid expression. (may not state ! on same item more than once)");
                                }
                                state = state | ExpressionState.HasNot;
                                continue;

                            case '&':
                            case ',':
                            case '\\':
                            case '/':
                                if (state > ExpressionState.HasOperator) {
                                    throw new ClrPlusException("Invalid expression. (May not state two operators in a row)");
                                }
                                state = state | ExpressionState.HasAnd;
                                continue;

                            case '|':
                            case '+':
                                if (state > ExpressionState.HasOperator) {
                                    throw new ClrPlusException("Invalid expression. (May not state two operators in a row)");
                                }
                                state = state | ExpressionState.HasOr;
                                continue;

                            case '(':
                                if (item.EndsWith(")")) {
                                    // parse nested expression.
                                    state = AppendExpression(template, result, state, template.Container.format(GenerateExpression(projectName, item.Substring(1, item.Length - 2), template, pivotsUsed)));
                                    continue;
                                }
                                throw new ClrPlusException("Mismatched '(' in expression");

                            default:
                                if (!WordRx.IsMatch(item)) {
                                    throw new ClrPlusException("Invalid characters in expression");
                                }
                                // otherwise, it's the word we're looking for.
                                // 
                                string choice;
                                Pivot pivot;

                                if (!GetChoice(item, out choice, out pivot)) {
                                    throw new ClrPlusException("Unmatched configuration choice '{0}".format(item));
                                }
                                if (!pivotsUsed.Contains(pivot)) {
                                    pivotsUsed.Add(pivot);
                                }
                                state = AppendExpression(template, result, state, template.Comparison(projectName, choice, pivot));
                                break;
                        }
                    }
                }
                // Event<Verbose>.Raise("Pivots.GenerateExpression", "result [{0}]", result.ToString());

                return result.ToString();
            }, projectName, expression, template);
            _canonicalExpressions.GetOrAdd(expression, () => new List<string>()).Add(theresult);
            
            var pU = _pivotsUsedCache.GetCachedAnswer(() => pivotsUsed.ToArray(), expression);
            if (pivotsUsed.Count == 0) {
                pivotsUsed.AddRange(pU);
            }
            return theresult;
        }

        private static ExpressionState AppendExpression(ExpressionTemplate template, StringBuilder result, ExpressionState state, string expr) {
            if (result.Length == 0) {
                switch (state) {
                    case ExpressionState.None:
                    case ExpressionState.HasAnd:
                        result.Append(expr);
                        state = ExpressionState.None;
                        break;

                    case ExpressionState.HasAnd | ExpressionState.HasNot:
                    case ExpressionState.HasNot:
                        result.Append(template.Not).Append(expr);
                        state = ExpressionState.None;
                        break;

                    case ExpressionState.HasOr:
                    case ExpressionState.HasOr | ExpressionState.HasNot:
                        throw new ClrPlusException("Invalid Conditional Expression (starts with 'or' operator?) ");
                }
            } else {
                switch (state) {
                    case ExpressionState.None:
                    case ExpressionState.HasNot:
                        throw new ClrPlusException("Invalid Conditional Expression (missing comparison before value) ");

                    case ExpressionState.HasAnd:
                        result.Append(template.And).Append(expr);
                        state = ExpressionState.None;
                        break;

                    case ExpressionState.HasAnd | ExpressionState.HasNot:
                        result.Append(template.AndNot).Append(expr);
                        state = ExpressionState.None;
                        break;

                    case ExpressionState.HasOr:
                        result.Append(template.Or).Append(expr);
                        state = ExpressionState.None;
                        break;

                    case ExpressionState.HasOr | ExpressionState.HasNot:
                        result.Append(template.OrNot).Append(expr);
                        state = ExpressionState.None;
                        break;
                }
            }
            return state;
        }

        private bool GetChoice(string item, out string choice, out Pivot pivot) {
            foreach (var p in _pivots.Keys) {
                pivot = _pivots[p];
                if (pivot.Choices.Keys.Contains(item)) {
                    choice = item;
                    return true;
                }
            }

            foreach(var p in _pivots.Keys) {
                pivot = _pivots[p];
                choice = _pivots[p].Choices.Keys.FirstOrDefault(ch => _pivots[p].Choices[ch].Contains(item.ToLower()));
                
                if (choice != null) {
                    return true;
                }
            }

            choice = null;
            pivot = null;
            return false;
        }


        public override bool Remove(string key) {
            throw new NotImplementedException();
        }

        [Flags]
        private enum ExpressionState {
            None = 0,
            HasOperator = 1,
            HasAnd = 2,
            HasOr = 4,
            HasNot = 8,
        }

        public class Pivot {
            internal Dictionary<string, string> Descriptions = new Dictionary<string, string>();
            internal Dictionary<string, IEnumerable<string>> Choices = new Dictionary<string, IEnumerable<string>>();
            internal Dictionary<string, string> Conditions = new Dictionary<string, string>();
            
            private string _key;
            internal string Description;
            internal string EnumCode;
            internal string Key { get {
                return _key;
            } set {
                _key = value;
                IsBuiltIn = _key.Is();
            }}
            internal string Name;
            internal HashSet<string> UsedChoices = new HashSet<string>();

            internal bool IsBuiltIn;
        }
    }
}