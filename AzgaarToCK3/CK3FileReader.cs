using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AzgaarToCK3;

public static class CK3FileReader
{
    private enum TokenType
    {
        unknown,
        key,
        value,

        assignment,
        objectStart,
        objectEnd,
    }

    private class IncompleteToken 
    {
        public TokenType type;
        public string? value;

        public override string ToString()
        {
            return $"{type}: {value}";
        }
    }

    private class SemiCompleteToken
    {
        public List<(string, SemiCompleteToken)> KeyObjects = new();
        public List<(string, string)> KeyValues = new();
        public List<SemiCompleteToken> ArrayObjects = new();
        public List<string> ArrayValues = new();
    }

    private static IncompleteToken[] GetListOfTokens(string content)
    {
        var tokenRegex = new Regex(@"[\s\n]*([^\s\n]+)", RegexOptions.Compiled);
        var commentRegex = new Regex(@"[\s\n]*#[^\n]+", RegexOptions.Compiled);

        List<IncompleteToken> incompleteTokens = new();

        TokenType previousToken = TokenType.unknown;
        int cursor = 0;

        do
        {
            var leftContent = content[cursor..];
            var token = tokenRegex.Match(leftContent);
            var tokenUntrim = token.Groups[0].Value;
            cursor += tokenUntrim.Length;
            var tokenTrim = token.Groups[1].Value;

            switch (tokenTrim[0])
            {
                case '{':
                    {
                        if (previousToken is TokenType.assignment or TokenType.objectStart or TokenType.objectEnd)
                            AddIncompleteToken(TokenType.objectStart, null);
                        else
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                case '}':
                    {
                        if (previousToken is TokenType.value or TokenType.objectEnd)
                            AddIncompleteToken(TokenType.objectEnd, null);
                        else if (previousToken is TokenType.key)
                        {
                            ChangePreviousTokenType(TokenType.value);
                            AddIncompleteToken(TokenType.objectEnd, null);
                        }
                        else
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                case '#':
                    {
                        // Skip comment.
                        var comment = commentRegex.Match(leftContent).Groups[0].Value;
                        cursor += comment.Length - tokenUntrim.Length;
                        break;
                    }
                case '=':
                    {
                        if (previousToken is TokenType.key)
                            previousToken = TokenType.assignment;
                        else
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                case '\"':
                    {
                        if (previousToken is TokenType.assignment)
                            AddIncompleteToken(TokenType.value, tokenTrim);
                        else
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                case var tokenBeginning when Char.IsLetter(tokenBeginning):
                    {
                        if (previousToken is TokenType.unknown or TokenType.objectStart or TokenType.value or TokenType.objectEnd)
                            AddIncompleteToken(TokenType.key, tokenTrim);
                        else if (previousToken is TokenType.assignment)
                            AddIncompleteToken(TokenType.value, tokenTrim);
                        else if (previousToken is TokenType.key)
                        {
                            ChangePreviousTokenType(TokenType.value);
                            AddIncompleteToken(TokenType.value, tokenTrim);
                        }
                        else
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                case var tokenBeginning when Char.IsDigit(tokenBeginning):
                    {
                        if (previousToken is TokenType.objectStart or TokenType.value or TokenType.assignment)
                            AddIncompleteToken(TokenType.value, tokenTrim);
                        else if (previousToken is TokenType.key)
                        {
                            // Change previous token to value. It wasn't a field name after all.
                            ChangePreviousTokenType(TokenType.value);
                            AddIncompleteToken(TokenType.value, tokenTrim);
                        }
                        else if (previousToken is TokenType.unknown)
                            throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                        break;
                    }
                default:
                    break;
            }

        } while (cursor < content.Length - 1);

        return incompleteTokens.ToArray();

        void AddIncompleteToken(TokenType type, string? value)
        {
            previousToken = type;
            incompleteTokens.Add(new IncompleteToken
            {
                type = type,
                value = value
            });
        }

        void ChangePreviousTokenType(TokenType type)
        {
            incompleteTokens.Last().type = type;
        }
    }
    private static SemiCompleteToken BuildTokenTree(IncompleteToken[] incompleteTokens)
    {
        var root = new SemiCompleteToken();
        Stack<SemiCompleteToken> completeTokens = new();

        var lastToken = root;
        string? lastKey = null;
        foreach (var token in incompleteTokens)
        {
            switch (token.type)
            {
                case TokenType.key:
                    lastKey = token.value;
                    break;
                case TokenType.value:
                    {
                        if (lastKey is not null)
                        {
                            lastToken.KeyValues.Add((lastKey, token.value!));
                            lastKey = null;
                        }
                        else
                        {
                            lastToken.ArrayValues.Add(token.value!);
                        }
                        break;
                    }
                case TokenType.objectStart:
                    {
                        if (lastKey is not null)
                        {
                            completeTokens.Push(lastToken);
                            var newToken = new SemiCompleteToken();
                            lastToken.KeyObjects.Add((lastKey, newToken));
                            lastToken = newToken;
                            lastKey = null;
                        }
                        else
                        {
                            completeTokens.Push(lastToken);
                            var newToken = new SemiCompleteToken();
                            lastToken.ArrayObjects.Add(newToken);
                            lastToken = newToken;
                            lastKey = null;
                        }
                        break;
                    }
                case TokenType.objectEnd:
                    {
                        var oldToken = completeTokens.Pop();
                        lastToken = oldToken;
                        break;
                    }
                default:
                    throw new ArgumentException($"unexpected token type {token.type}");
            }
        }

        return root;
    }

    // Head recursion. Bevare of stack overflow.
    private static Dictionary<string, object[]> GetCompleteTokens(SemiCompleteToken token)
    {
        var completeToken = new Dictionary<string, object[]>();
        token.KeyValues.GroupBy(n => n.Item1).ToList()
            .ForEach(n => { completeToken[n.Key] = n.Select(n => n.Item2).ToArray(); });
     
        token.KeyObjects.GroupBy(n => n.Item1).ToList()
          .ForEach(n => { completeToken[n.Key] = n.Select(m => GetCompleteTokens(m.Item2)).Cast<object>().ToArray(); });

        completeToken["values"] = token.ArrayValues.Cast<object>().Concat(token.ArrayObjects.Select(n => GetCompleteTokens(n))).ToArray();

        return completeToken;
    }

    public static Dictionary<string, object[]> Read(string filename)
    {
        var content = File.ReadAllText(filename);
     
        try
        {
            var incompleteTokens = GetListOfTokens(content);
            var semicompleteToken = BuildTokenTree(incompleteTokens);
            var completeToken = GetCompleteTokens(semicompleteToken);

            return completeToken;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
}
