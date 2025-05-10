using UnityEngine;
using UnityEngine.Assertions;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;


namespace Linda.Plants
{
	public class LSystem
	{
		string[] variables;
		string[] functions;
		char[] constants;
		System.Object behavior;

		public LSystem(string[] variables, string[] functions, char[] constants, System.Object behavior)
		{
			this.variables = variables;
			this.functions = functions;
			this.constants = constants;
			this.behavior = behavior;
		}

		public List<Token> Build(string axiom, List<(string, string)> ruleExprs, int iterations)
		{
			// parse rules
			List<Rule> rules = new List<Rule>();
			foreach (var ruleExpr in ruleExprs)
			{
				Rule rule = new Rule();
				rule.Predecessor = Tokenize(ruleExpr.Item1);
				rule.Successor = Tokenize(ruleExpr.Item2);
				rules.Add(rule);
			}

			// run iterations
			List<Token> current = Tokenize(axiom);
			for (int iter = 0; iter < iterations; iter++)
			{
				List<Token> next = new List<Token>();
				while (current.Count > 0)
				{
					// find matching rule
					bool bRuleMatch = false;
					foreach (var rule in rules)
					{
						bRuleMatch = true;
						List<Token> predecessor = new List<Token>(rule.Predecessor);
						for (int pi = 0; pi < predecessor.Count; pi++)
						{
							if (current[pi].Name != predecessor[pi].Name)
							{
								bRuleMatch = false;
								break;
							}
						}
						if (bRuleMatch)
						{
							// replace parameters in successor
							List<Token> successor = rule.Successor.Select(token => token.Clone()).ToList(); ;
							for (int si = 0; si < successor.Count; si++)
							{
								if (successor[si].Args == null) continue;
								for (int pi = 0; pi < predecessor.Count; pi++)
								{
									if (predecessor[pi].Args == null) continue;
									for (int pai = 0; pai < predecessor[pi].Args.Length; pai++)
									{
										string replacingPattern = @"(?<![a-zA-Z0-9_])" + predecessor[pi].Args[pai] + "(?![a-zA-Z0-9_])";
										string replaceValue = current[pi].Args[pai];
										for (int sai = 0; sai < successor[si].Args.Length; sai++)
										{
											successor[si].Args[sai] = Regex.Replace(successor[si].Args[sai], replacingPattern, replaceValue);
										}
									}
								}
								for (int sai = 0; sai < successor[si].Args.Length; sai++)
								{
									successor[si].Args[sai] = evaluateExpression(successor[si].Args[sai]);
								}
							}
							// replace next with successor
							current.RemoveRange(0, predecessor.Count);
							next.AddRange(successor);
							break;
						}
					}
					// if no rule matched, just copy the token
					if (bRuleMatch == false)
					{
						next.Add(current[0]);
						current.RemoveAt(0);
					}
				}
				current = next;
			}

			return current;
		}

		public void Execute(List<Token> tokens)
		{
			Type behaviorType = behavior.GetType();
			MethodInfo initMethod = behaviorType.GetMethod("Init");
			initMethod.Invoke(behavior, null);

			foreach (var token in tokens)
			{
				string name = token.Name;
				string[] argStrings = token.Args;

				if (name.Length == 1 && constants.Contains(name[0]))
				{
					MethodInfo method = behaviorType.GetMethod("ParseConstant");
					method.Invoke(behavior, new object[] { name[0] });
				}
				else if (variables.Contains(name))
				{
					MethodInfo method = behaviorType.GetMethod(name);
					if (method != null)
					{
						ParameterInfo[] parameters = method.GetParameters();
						object[] args = null;
						if (argStrings != null)
						{
							args = new object[parameters.Length];
							for (int i = 0; i < parameters.Length; i++)
							{
								args[i] = Convert.ChangeType(argStrings[i], parameters[i].ParameterType);
							}
						}
						method.Invoke(behavior, args);
					}
				}
				else
				{
					Assert.IsTrue(false, name + " is not a valid variable.");
				}
			}
		}

		public static void Print(List<Token> tokens)
		{
			foreach (var token in tokens)
			{
				if (token.Args != null)
				{
					Console.Write(token.Name);
					Console.Write("(");
					foreach (var arg in token.Args)
					{
						Console.Write(arg);
						if (arg != token.Args[token.Args.Length - 1])
						{
							Console.Write(",");
						}
					}
					Console.Write(")");
				}
				else
				{
					Console.Write(token.Name);
				}
			}
			Console.WriteLine();
		}

		(Token, string) parseExpression(string input)
		{
			Token token = new Token();
			string remaining;

			if (input.IndexOfAny(constants) == 0)
			{
				token.Name = input[0].ToString();
				token.Args = null;
				remaining = input.Substring(1).Trim();
				return (token, remaining);
			}

			foreach (var variable in variables)
			{
				if (input.StartsWith(variable))
				{
					input = input.Substring(variable.Length).Trim();
					if (input.Length > 0 && input[0] == '(')
					{
						int idx = 0;
						int depth = 0;
						for (; idx < input.Length; ++idx)
						{
							if (input[idx] == '(') depth++;
							else if (input[idx] == ')') depth--;

							if (depth == 0) break;
						}
						Debug.Assert(depth == 0);
						string paramPart = input.Substring(1, idx - 1).Trim();

						token.Name = variable;
						token.Args = splitParameters(paramPart);

						remaining = input.Substring(idx + 1).Trim();
						return (token, remaining);
					}
					else
					{
						token.Name = variable;
						token.Args = null;
						remaining = input;
						return (token, remaining);
					}
				}
			}
			token.Name = input;
			token.Args = null;
			remaining = input.Substring(token.Name.Length).Trim();
			return (token, remaining);
		}

		public List<Token> Tokenize(string expr)
		{
			List<Token> tokens = new List<Token>();
			while (expr.Length > 0)
			{
				Token token;
				(token, expr) = parseExpression(expr);
				tokens.Add(token);
			}

			return tokens;
		}

		public string[] splitParameters(string input)
		{
			List<string> parameters = new List<string>();
			string curr = input;
			int splitIdx = 0;
			while (curr.Length > 0)
			{
				bool bFound = false;
				foreach (var function in functions)
				{
					if (curr.Substring(splitIdx).StartsWith(function))
					{
						splitIdx += function.Length;
						int depth = 0;
						for (; splitIdx < curr.Length; ++splitIdx)
						{
							if (curr[splitIdx] == '(') depth++;
							else if (curr[splitIdx] == ')') depth--;

							if (depth == 0) break;
						}
						++splitIdx;
						bFound = true;
						break;
					}
				}
				if (!bFound)
				{
					++splitIdx;
				}

				if (splitIdx == curr.Length)
				{
					parameters.Add(curr.Substring(0, splitIdx));
					break;
				}

				if (input[splitIdx] == ',')
				{
					parameters.Add(curr.Substring(0, splitIdx));
					curr = curr.Substring(++splitIdx);
					splitIdx = 0;
				}
			}

			return parameters.ToArray();
		}

		string evaluateExpression(string expr)
		{
			Func<string, string> recursUntilComputable = null;
			recursUntilComputable = (string e) =>
			{
				List<(int, int, int)> functionRanges = findFunction(e);
				if (functionRanges.Count == 0)
				{
					var table = new DataTable();
					e = Regex.Replace(e, @"(\d)\(", "$1*("); // add * between number and (
					return table.Compute(e, "").ToString();
				}

				string retExpr = "";
				int prevEnd = 0;
				foreach (var (start, paramStart, paramEnd) in functionRanges)
				{
					retExpr += e.Substring(prevEnd, start);

					string functionName = e.Substring(start, paramStart - start - 1);
					string paramPart = e.Substring(paramStart, paramEnd - paramStart);
					string[] paramStrings = splitParameters(paramPart);

					for (int pi = 0; pi < paramStrings.Length; ++pi)
					{
						paramStrings[pi] = recursUntilComputable(paramStrings[pi]);
					}

					Type behaviorType = behavior.GetType();
					MethodInfo method = behaviorType.GetMethod(functionName);
					ParameterInfo[] parameters = method.GetParameters();

					object[] args = null;
					if (paramStrings != null)
					{
						args = new object[parameters.Length];
						for (int i = 0; i < parameters.Length; i++)
						{
							Type paramType = parameters[i].ParameterType;
							bool isNumericType =
								paramType == typeof(byte) || paramType == typeof(sbyte) ||
								paramType == typeof(short) || paramType == typeof(ushort) ||
								paramType == typeof(int) || paramType == typeof(uint) ||
								paramType == typeof(long) || paramType == typeof(ulong) ||
								paramType == typeof(float) || paramType == typeof(double) ||
								paramType == typeof(decimal);
							if (isNumericType)
							{
								double temp = Convert.ToDouble(paramStrings[i]);
								args[i] = Convert.ChangeType(temp, parameters[i].ParameterType);
							}
							else
							{
								args[i] = Convert.ChangeType(paramStrings[i], parameters[i].ParameterType);
							}
						}
					}

					string ret = method.Invoke(behavior, args).ToString();
					retExpr += ret;
					prevEnd = paramEnd + 1;
				}
				retExpr += e.Substring(prevEnd);
				return retExpr;
			};

			string result = recursUntilComputable(expr);
			return result;
		}

		List<(int, int, int)> findFunction(string expr)
		{
			List<(int, int, int)> functionRanges = new List<(int, int, int)>();

			int idx = 0;
			while (idx < expr.Length)
			{
				bool bFound = false;
				foreach (var function in functions)
				{
					if (expr.Substring(idx).StartsWith(function))
					{
						int start = idx;
						idx += function.Length;
						int paramStart = idx + 1;
						int depth = 0;
						for (; idx < expr.Length; ++idx)
						{
							if (expr[idx] == '(') depth++;
							else if (expr[idx] == ')') depth--;
							if (depth == 0) break;
						}
						Debug.Assert(depth == 0);
						functionRanges.Add((start, paramStart, idx));
						bFound = true;
						break;
					}
				}
				if (!bFound)
				{
					++idx;
				}
			}

			return functionRanges;
		}
	}
}