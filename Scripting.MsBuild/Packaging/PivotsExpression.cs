namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Core.Exceptions;

    public class PivotsExpression {
        internal PivotsExpression() {
        }

        internal PivotsExpression(bool invariant_result) {
            invariant = true;
            this.invariant_result = invariant_result;
        }

        public static readonly PivotsExpression True = new PivotsExpression(true);
        public static readonly PivotsExpression False = new PivotsExpression(false);

        internal bool invariant; // If true, this expression is always true or always false
        internal bool invariant_result;
        internal ComparableHashSet<ComparableHashSet<string>> matching_combinations;
        internal HashSet<string> relevant_pivots;

        public bool IsAlwaysTrue {
            get {
                return invariant && invariant_result;
            }
        }

        public bool IsAlwaysFalse {
            get {
                return invariant && !invariant_result;
            }
        }

        public bool IsInvariant {
            get {
                return invariant;
            }
        }

        public override bool Equals(object obj) {
            if (obj is PivotsExpression) {
                PivotsExpression expr = obj as PivotsExpression;
                if (expr.invariant != this.invariant)
                    return false;
                if (this.invariant)
                    return expr.invariant_result == this.invariant_result;
                return matching_combinations.Equals(expr.matching_combinations);
            }
            return base.Equals(obj);
        }

        public override string ToString() {
            if (invariant) {
                if (invariant_result)
                    return "PivotsExpression.TrueExpression";
                else
                    return "PivotsExpression.FalseExpression";
            }
            StringBuilder res = new StringBuilder();
            bool first_combination = true;
            foreach (var combination in matching_combinations) {
                if (!first_combination)
                    res.Append("|");
                res.Append("(");
                bool first_value = true;
                foreach (var item in combination) {
                    if (!first_value)
                        res.Append(",");
                    res.Append(item);
                    first_value = false;
                }
                res.Append(")");
                first_combination = false;
            }
            return res.ToString();
        }

        public override int GetHashCode() {
            if (invariant)
                return invariant_result ? 132581999 : 1549646950;
            return matching_combinations.GetHashCode();
        }

        public IEnumerable<IEnumerable<string>> GetCombinations() {
            return matching_combinations;
        }

        public delegate bool GetChoiceDelegate(string item, out string choice, out string pivotName);

        private abstract class AstExpression {
            abstract internal AstExpression Invert();
            abstract internal PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn);
        }

        private class TrueExpression : AstExpression {
            static internal readonly TrueExpression instance = new TrueExpression();

            internal override AstExpression Invert() {
                return FalseExpression.instance;
            }

            internal override PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn) {
                return PivotsExpression.True;
            }
        }

        private class FalseExpression : AstExpression {
            static internal readonly FalseExpression instance = new FalseExpression();

            internal override AstExpression Invert() {
                return TrueExpression.instance;
            }

            internal override PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn) {
                return PivotsExpression.False;
            }
        }

        private class OrExpression : AstExpression {
            internal AstExpression left_child;
            internal AstExpression right_child;

            internal OrExpression(AstExpression left_child, AstExpression right_child) {
                this.left_child = left_child;
                this.right_child = right_child;
            }

            internal override AstExpression Invert() {
                return new AndExpression(left_child.Invert(), right_child.Invert());
            }

            internal override PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn) {
                PivotsExpression left, right;

                left = left_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                if (left.invariant) {
                    if (left.invariant_result)
                        return left;
                    else
                        return right_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                }
                right = right_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                if (right.invariant) {
                    if (right.invariant_result)
                        return right;
                    else
                        return left;
                }

                HashSet<string> pivots;
                ComparableHashSet<ComparableHashSet<string>> result;

                pivots = new HashSet<string>(left.relevant_pivots);
                pivots.UnionWith(right.relevant_pivots);

                result = GetExpandedChoices(left, pivots, pivotVsPivotValues);
                result.UnionWith(GetExpandedChoices(right, pivots, pivotVsPivotValues));

                PivotsExpression res = new PivotsExpression();
                res.relevant_pivots = pivots;
                res.matching_combinations = result;
                return res.Simplify(pivotVsPivotValues);
            }

            internal ComparableHashSet<ComparableHashSet<string>> GetExpandedChoices(PivotsExpression expr, HashSet<string> pivots, Dictionary<string, Pivots.Pivot> pivotVsPivotValues) {
                ComparableHashSet<ComparableHashSet<string>> result = expr.matching_combinations;
                foreach (string pivot in pivots) {
                    if (expr.relevant_pivots.Contains(pivot))
                        continue;
                    ComparableHashSet<ComparableHashSet<string>> new_result = new ComparableHashSet<ComparableHashSet<string>>();
                    var pivot_choices = pivotVsPivotValues[pivot].Choices.Keys;
                    foreach (var matching_combination in result) {
                        foreach (string pivot_choice in pivot_choices) {
                            ComparableHashSet<string> choices = new ComparableHashSet<string>(matching_combination);
                            choices.Add(pivot_choice);
                            new_result.Add(choices);
                        }
                    }
                    result = new_result;
                }
                return result;
            }
        }

        private class AndExpression : AstExpression {
            internal AstExpression left_child;
            internal AstExpression right_child;

            internal AndExpression(AstExpression left_child, AstExpression right_child) {
                this.left_child = left_child;
                this.right_child = right_child;
            }

            internal override AstExpression Invert() {
                return new OrExpression(left_child.Invert(), right_child.Invert());
            }

            internal override PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn) {
                PivotsExpression left, right;

                left = left_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                if (left.invariant) {
                    if (left.invariant_result)
                        return right_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                    else
                        return left;
                }
                right = right_child.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
                if (right.invariant) {
                    if (right.invariant_result)
                        return left;
                    else
                        return right;
                }

                HashSet<string> shared_pivots = new HashSet<string>(left.relevant_pivots);
                shared_pivots.IntersectWith(right.relevant_pivots);

                if (shared_pivots.Count == left.relevant_pivots.Count) {
                    // All pivots are shared, just intersect the sets.
                    PivotsExpression result = new PivotsExpression();
                    result.relevant_pivots = left.relevant_pivots;
                    result.matching_combinations = new ComparableHashSet<ComparableHashSet<string>>(left.matching_combinations);
                    result.matching_combinations.IntersectWith(right.matching_combinations);
                    return result.Simplify(pivotVsPivotValues);
                }

                if (shared_pivots.Count == 0) {
                    // No shared pivots, so do a cross product
                    PivotsExpression result = new PivotsExpression();
                    result.relevant_pivots = new HashSet<string>(left.relevant_pivots);
                    result.relevant_pivots.UnionWith(right.relevant_pivots);

                    result.matching_combinations = new ComparableHashSet<ComparableHashSet<string>>();

                    foreach (var left_combination in left.matching_combinations) {
                        foreach (var right_combination in right.matching_combinations) {
                            ComparableHashSet<string> new_combination = new ComparableHashSet<string>(left_combination);
                            new_combination.UnionWith(right_combination);
                            result.matching_combinations.Add(new_combination);
                        }
                    }
                    // It shouldn't be necessary to simplify in this case, as any independent pivots should have been removed already
                    return result;
                }

                HashSet<string> shared_pivot_values = new HashSet<string>();
                foreach (string pivot in shared_pivots) {
                    shared_pivot_values.UnionWith(pivotVsPivotValues[pivot].Choices.Keys);
                }

                // Sort by relevant pivot count
                if (left.relevant_pivots.Count > right.relevant_pivots.Count) {
                    var tmp = left;
                    left = right;
                    right = tmp;
                }

                if (right.relevant_pivots.IsSupersetOf(left.relevant_pivots)) {
                    // Filter the combintions in right by what's in left.
                    PivotsExpression result = new PivotsExpression();
                    result.relevant_pivots = right.relevant_pivots;

                    result.matching_combinations = new ComparableHashSet<ComparableHashSet<string>>();

                    foreach (var right_combination in right.matching_combinations) {
                        ComparableHashSet<string> reduced_combination = new ComparableHashSet<string>(right_combination);
                        reduced_combination.IntersectWith(shared_pivot_values);
                        if (left.matching_combinations.Contains(reduced_combination))
                            result.matching_combinations.Add(right_combination);
                    }
                    return result.Simplify(pivotVsPivotValues);
                }
                else {
                    Dictionary<ComparableHashSet<string>, List<ComparableHashSet<string>>> shared_values_to_left_values = new Dictionary<ComparableHashSet<string>, List<ComparableHashSet<string>>>();

                    foreach (var left_combination in left.matching_combinations) {
                        ComparableHashSet<string> shared_values = new ComparableHashSet<string>();
                        foreach (var value in left_combination) {
                            if (shared_pivot_values.Contains(value))
                                shared_values.Add(value);
                        }
                        List<ComparableHashSet<string>> combination_list;
                        if (!shared_values_to_left_values.TryGetValue(shared_values, out combination_list))
                            combination_list = shared_values_to_left_values[shared_values] = new List<ComparableHashSet<string>>();
                        combination_list.Add(left_combination);
                    }

                    PivotsExpression result = new PivotsExpression();

                    result.relevant_pivots = new HashSet<string>(left.relevant_pivots);
                    result.relevant_pivots.UnionWith(right.relevant_pivots);

                    result.matching_combinations = new ComparableHashSet<ComparableHashSet<string>>();

                    foreach (var right_combination in right.matching_combinations) {
                        ComparableHashSet<string> shared_values = new ComparableHashSet<string>();
                        foreach (var value in right_combination) {
                            if (shared_pivot_values.Contains(value))
                                shared_values.Add(value);
                        }
                        List<ComparableHashSet<string>> left_combinations;
                        if (shared_values_to_left_values.TryGetValue(shared_values, out left_combinations)) {
                            foreach (var left_combination in left_combinations) {
                                ComparableHashSet<string> new_combination = new ComparableHashSet<string>(right_combination);
                                new_combination.UnionWith(left_combination);
                                result.matching_combinations.Add(new_combination);
                            }
                        }
                    }
                    return result.Simplify(pivotVsPivotValues);
                }
            }
        }

        private class PivotExpression : AstExpression {
            internal string pivot_name;
            internal string pivot_choice;
            internal bool inverted;

            internal PivotExpression(string pivot_name, string pivot_choice, bool inverted) {
                this.pivot_name = pivot_name;
                this.pivot_choice = pivot_choice;
                this.inverted = inverted;
            }

            internal override AstExpression Invert() {
                return new PivotExpression(pivot_name, pivot_choice, !inverted);
            }

            internal override PivotsExpression ToPivotsExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues, GetChoiceDelegate get_choice_fn) {
                var result = new PivotsExpression();

                result.relevant_pivots = new HashSet<string>();
                result.relevant_pivots.Add(pivot_name);

                result.matching_combinations = new ComparableHashSet<ComparableHashSet<string>>();

                if (inverted) {
                    foreach (string pivotValue in pivotVsPivotValues[pivot_name].Choices.Keys) {
                        if (pivotValue != pivot_choice) {
                            ComparableHashSet<string> choice = new ComparableHashSet<string>();
                            choice.Add(pivotValue);
                            result.matching_combinations.Add(choice);
                        }
                    }
                }
                else {
                    ComparableHashSet<string> choice = new ComparableHashSet<string>();
                    choice.Add(pivot_choice);
                    result.matching_combinations.Add(choice);
                }

                return result;
            }
        }
#if NOT_COMMON
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
      \\+
      |
      \,+
      |
      !+
      |
      \w+
      |
      .*
      )*\s*$", RegexOptions.IgnorePatternWhitespace);
#endif

        private enum ExpressionState {
            None,
            HasAnd,
            HasOr,
        }

        private static AstExpression ParseExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues,
            GetChoiceDelegate get_choice_fn, string expression) {
            AstExpression result = null;
            var rxResult = Pivots.ExpressionRx.Match(expression);
            if (rxResult.Success) {
                var state = ExpressionState.None;
                AstExpression current = null;
                bool invert = false;

                foreach (var item in rxResult.Groups[1].Captures.Cast<Capture>().Select(each => each.Value.Trim()).Where(each => !string.IsNullOrEmpty(each))) {
                    switch (item[0]) {
                        case '!':
                            if (result != null && state == ExpressionState.None) {
                                throw new ClrPlusException("Invalid expression. (not expression must be separated from previous expression with an operator)");
                            }
                            if (item.Length % 2 != 0)
                                invert = !invert;
                            continue;

                        case '&':
                        case ',':
                        case '\\':
                        case '/':
                            if (state != ExpressionState.None) {
                                throw new ClrPlusException("Invalid expression. (May not state two operators in a row)");
                            }
                            if (result == null) {
                                throw new ClrPlusException("Invalid expression. (may not start with an operator)");
                            }
                            state = ExpressionState.HasAnd;
                            continue;

                        case '|':
                        case '+':
                            if (state != ExpressionState.None) {
                                throw new ClrPlusException("Invalid expression. (May not state two operators in a row)");
                            }
                            if (result == null) {
                                throw new ClrPlusException("Invalid expression. (may not start with an operator)");
                            }
                            state = ExpressionState.HasOr;
                            continue;

                        case '(':
                            if (result != null && state == ExpressionState.None) {
                                throw new ClrPlusException("Invalid expression. (nested expression must be separated from previous expression with an operator)");
                            }
                            if (item.EndsWith(")")) {
                                // parse nested expression.
                                current = ParseExpression(pivotVsPivotValues, get_choice_fn, item.Substring(1, item.Length - 2));
                                break;
                            }
                            throw new ClrPlusException("Mismatched '(' in expression");

                        default:
                            if (!Pivots.WordRx.IsMatch(item)) {
                                throw new ClrPlusException("Invalid characters in expression");
                            }
                            if (result != null && state == ExpressionState.None) {
                                throw new ClrPlusException("Invalid expression. (expression must be separated from previous expression with an operator)");
                            }
                            // otherwise, it's the word we're looking for.
                            // 
                            string choice;
                            string pivot;

                            if (get_choice_fn(item, out choice, out pivot)) {
                                current = new PivotExpression(pivot, choice, false);
                                break;
                            }
                            else if (item.ToLowerInvariant() == "true") {
                                current = TrueExpression.instance;
                                break;
                            }
                            else if (item.ToLowerInvariant() == "false") {
                                current = FalseExpression.instance;
                                break;
                            }

                            throw new ClrPlusException(string.Format("Unmatched configuration choice '{0}", item));
                    }

                    if (invert)
                        current = current.Invert();

                    switch (state) {
                        case ExpressionState.None:
                            result = current;
                            continue;
                        case ExpressionState.HasAnd:
                            result = new AndExpression(result, current);
                            break;
                        case ExpressionState.HasOr:
                            result = new OrExpression(result, current);
                            break;
                    }

                    current = null;
                    state = ExpressionState.None;
                }
            }

            if (result == null)
                result = TrueExpression.instance;

            return result;
        }

        public static PivotsExpression ReadExpression(Dictionary<string, Pivots.Pivot> pivotVsPivotValues,
            GetChoiceDelegate get_choice_fn, string expression) {
            AstExpression expr = ParseExpression(pivotVsPivotValues, get_choice_fn, expression);

            return expr.ToPivotsExpression(pivotVsPivotValues, get_choice_fn);
        }

        internal PivotsExpression Simplify(Dictionary<string, Pivots.Pivot> pivotVsPivotValues) {
            if (invariant)
                return this;
            if (matching_combinations.Count == 0)
                return False;
            int possible_combinations = 1;
            foreach (string pivot in relevant_pivots) {
                possible_combinations *= pivotVsPivotValues[pivot].Choices.Keys.Count;
            }
            if (matching_combinations.Count == possible_combinations)
                return True;
            foreach (string pivot in relevant_pivots) {
                var values = pivotVsPivotValues[pivot].Choices.Keys;
                if (matching_combinations.Count % values.Count != 0)
                    continue;
                int reduced_combinations = matching_combinations.Count / values.Count;
                ComparableHashSet<ComparableHashSet<string>> new_combinations = new ComparableHashSet<ComparableHashSet<string>>();
                foreach (var combination in matching_combinations) {
                    ComparableHashSet<string> reduced_combination = new ComparableHashSet<string>(combination);
                    reduced_combination.ExceptWith(values);
                    new_combinations.Add(reduced_combination);
                    if (new_combinations.Count > reduced_combinations)
                        break;
                }
                if (new_combinations.Count == reduced_combinations) {
                    PivotsExpression result = new PivotsExpression();
                    result.relevant_pivots = new HashSet<string>(relevant_pivots);
                    result.relevant_pivots.Remove(pivot);
                    result.matching_combinations = new_combinations;
                    return result.Simplify(pivotVsPivotValues);
                }
            }
            return this;
        }
    }
}