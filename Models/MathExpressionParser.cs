using System;
using System.Collections.Generic;
using System.Globalization;

namespace DifferentialEquation.Models;

public static class MathExpressionParser
{
    private enum TokenType
    {
        Number,
        Identifier,
        Operator,
        LeftParen,
        RightParen
    }

    private sealed record Token(TokenType Type, string Value);

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sin", "cos", "tan", "asin", "acos", "atan",
        "sinh", "cosh", "tanh", "exp", "ln", "log", "sqrt", "abs"
    };

    public static double Evaluate(string expression, double x)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Function expression cannot be empty.");
        }

        List<Token> tokens = Tokenize(expression);
        int position = 0;
        double result = ParseExpression(tokens, ref position, x);

        if (position < tokens.Count)
        {
            throw new FormatException($"Unexpected token '{tokens[position].Value}'.");
        }

        return result;
    }

    public static double FirstDerivative(string expression, double x)
    {
        double h = 1e-5;
        double fPlus2 = Evaluate(expression, x + 2 * h);
        double fPlus1 = Evaluate(expression, x + h);
        double fMinus1 = Evaluate(expression, x - h);
        double fMinus2 = Evaluate(expression, x - 2 * h);

        return (-fPlus2 + 8 * fPlus1 - 8 * fMinus1 + fMinus2) / (12 * h);
    }

    public static double SecondDerivative(string expression, double x)
    {
        double h = 1e-4;
        double fPlus2 = Evaluate(expression, x + 2 * h);
        double fPlus1 = Evaluate(expression, x + h);
        double f0 = Evaluate(expression, x);
        double fMinus1 = Evaluate(expression, x - h);
        double fMinus2 = Evaluate(expression, x - 2 * h);

        return (-fPlus2 + 16 * fPlus1 - 30 * f0 + 16 * fMinus1 - fMinus2) / (12 * h * h);
    }

    private static List<Token> Tokenize(string input)
    {
        string s = input.Replace(" ", "").ToLowerInvariant();
        List<Token> raw = new();
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool hasDot = false;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                {
                    if (s[i] == '.')
                    {
                        if (hasDot)
                        {
                            throw new FormatException("Invalid decimal format.");
                        }
                        hasDot = true;
                    }
                    i++;
                }
                raw.Add(new Token(TokenType.Number, s[start..i]));
            }
            else if (char.IsLetter(c))
            {
                int start = i;
                while (i < s.Length && char.IsLetter(s[i]))
                {
                    i++;
                }
                raw.Add(new Token(TokenType.Identifier, s[start..i]));
            }
            else if (c is '+' or '-' or '*' or '/' or '^')
            {
                raw.Add(new Token(TokenType.Operator, c.ToString()));
                i++;
            }
            else if (c == '(')
            {
                raw.Add(new Token(TokenType.LeftParen, "("));
                i++;
            }
            else if (c == ')')
            {
                raw.Add(new Token(TokenType.RightParen, ")"));
                i++;
            }
            else
            {
                throw new FormatException($"Unsupported character '{c}'.");
            }
        }

        List<Token> normalized = new();
        for (int idx = 0; idx < raw.Count; idx++)
        {
            Token current = raw[idx];
            if (idx > 0)
            {
                Token prev = raw[idx - 1];
                bool prevIsValue = prev.Type == TokenType.Number ||
                                   prev.Type == TokenType.RightParen ||
                                   (prev.Type == TokenType.Identifier && !Functions.Contains(prev.Value));
                bool currStartsValue = current.Type == TokenType.Number ||
                                       current.Type == TokenType.Identifier ||
                                       current.Type == TokenType.LeftParen;

                if (prevIsValue && currStartsValue)
                {
                    normalized.Add(new Token(TokenType.Operator, "*"));
                }
            }
            normalized.Add(current);
        }

        return normalized;
    }

    private static double ParseExpression(List<Token> tokens, ref int pos, double x)
    {
        double left = ParseTerm(tokens, ref pos, x);
        while (pos < tokens.Count && tokens[pos].Type == TokenType.Operator &&
               (tokens[pos].Value == "+" || tokens[pos].Value == "-"))
        {
            string op = tokens[pos++].Value;
            double right = ParseTerm(tokens, ref pos, x);
            left = op == "+" ? left + right : left - right;
        }
        return left;
    }

    private static double ParseTerm(List<Token> tokens, ref int pos, double x)
    {
        double left = ParseUnary(tokens, ref pos, x);
        while (pos < tokens.Count && tokens[pos].Type == TokenType.Operator &&
               (tokens[pos].Value == "*" || tokens[pos].Value == "/"))
        {
            string op = tokens[pos++].Value;
            double right = ParseUnary(tokens, ref pos, x);
            if (op == "/" && Math.Abs(right) < 1e-15)
            {
                throw new DivideByZeroException("Division by zero encountered in function evaluation.");
            }
            left = op == "*" ? left * right : left / right;
        }
        return left;
    }

    private static double ParseUnary(List<Token> tokens, ref int pos, double x)
    {
        if (pos < tokens.Count && tokens[pos].Type == TokenType.Operator)
        {
            if (tokens[pos].Value == "+")
            {
                pos++;
                return ParseUnary(tokens, ref pos, x);
            }
            if (tokens[pos].Value == "-")
            {
                pos++;
                return -ParseUnary(tokens, ref pos, x);
            }
        }
        return ParsePower(tokens, ref pos, x);
    }

    private static double ParsePower(List<Token> tokens, ref int pos, double x)
    {
        double baseVal = ParsePrimary(tokens, ref pos, x);
        if (pos < tokens.Count && tokens[pos].Type == TokenType.Operator && tokens[pos].Value == "^")
        {
            pos++;
            double exponent = ParseUnary(tokens, ref pos, x);
            return Math.Pow(baseVal, exponent);
        }
        return baseVal;
    }

    private static double ParsePrimary(List<Token> tokens, ref int pos, double x)
    {
        if (pos >= tokens.Count)
        {
            throw new FormatException("Unexpected end of expression.");
        }

        Token token = tokens[pos];

        if (token.Type == TokenType.Number)
        {
            pos++;
            return double.Parse(token.Value, CultureInfo.InvariantCulture);
        }

        if (token.Type == TokenType.LeftParen)
        {
            pos++;
            double inner = ParseExpression(tokens, ref pos, x);
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightParen)
            {
                throw new FormatException("Missing closing parenthesis ')'.");
            }
            pos++;
            return inner;
        }

        if (token.Type == TokenType.Identifier)
        {
            pos++;
            string name = token.Value;

            if (name == "x")
            {
                return x;
            }
            if (name == "pi")
            {
                return Math.PI;
            }
            if (name == "e")
            {
                return Math.E;
            }

            if (Functions.Contains(name))
            {
                if (pos >= tokens.Count || tokens[pos].Type != TokenType.LeftParen)
                {
                    throw new FormatException($"Function '{name}' must be followed by '('.");
                }
                pos++;
                double arg = ParseExpression(tokens, ref pos, x);
                if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightParen)
                {
                    throw new FormatException($"Missing closing parenthesis for '{name}'.");
                }
                pos++;

                return name switch
                {
                    "sin" => Math.Sin(arg),
                    "cos" => Math.Cos(arg),
                    "tan" => Math.Tan(arg),
                    "asin" => Math.Asin(arg),
                    "acos" => Math.Acos(arg),
                    "atan" => Math.Atan(arg),
                    "sinh" => Math.Sinh(arg),
                    "cosh" => Math.Cosh(arg),
                    "tanh" => Math.Tanh(arg),
                    "exp" => Math.Exp(arg),
                    "ln" => Math.Log(arg),
                    "log" => Math.Log10(arg),
                    "sqrt" => Math.Sqrt(arg),
                    "abs" => Math.Abs(arg),
                    _ => throw new FormatException($"Unknown function '{name}'.")
                };
            }

            throw new FormatException($"Unknown identifier '{name}'. Use 'x' as the variable.");
        }

        throw new FormatException($"Unexpected token '{token.Value}'.");
    }
}