using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Converter;


/// <summary>
/// This creates a very inconvenient structure.
/// Should be used if it's impossible to convert a file to a json.
/// Recursive structure:
/// [string key, object[] values]
/// value = [string key, object[] value]
/// Known issues: hsv attribute is parsed but is ignored later
/// </summary>
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
        public string? modifier;

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

    public const string ValuesKey = "values";
    private static readonly HashSet<Regex> Modifiers = [new Regex(@"[.\s]*(hsv)\s*(.+)?", RegexOptions.Compiled)];

    private static IncompleteToken[] GetListOfTokens(string content)
    {
        var tokenRegex = new Regex(@"[\s\n]*([^\s\n}]+|})", RegexOptions.Compiled);
        var commentRegex = new Regex(@"[\s\n]*#[^\n]+", RegexOptions.Compiled);

        List<IncompleteToken> incompleteTokens = [];

        TokenType previousToken = TokenType.unknown;
        int cursor = 0;

        do
        {
            var leftContent = content[cursor..];
            var token = tokenRegex.Match(leftContent);
            var tokenUntrim = token.Groups[0].Value;
            cursor += tokenUntrim.Length;
            var tokenTrim = token.Groups[1].Value;

            string? modifier = null;
            if (Modifiers.Select(n => n.Match(tokenTrim)).Where(n => n is not null).ToArray() is [{ Groups: [_, { Value: var match }, ..] groups }])
            {
                modifier = match;

                if (groups.Count == 3 && !string.IsNullOrWhiteSpace(groups[2].Value))
                {
                    tokenUntrim = groups[2].Value;
                    tokenTrim = tokenUntrim.Trim();
                }
                else
                {
                    // Modifier read. Don't parse it as a token.
                    continue;
                }
            }

            try
            {
                if (tokenTrim.Length is 0)
                {
                    if (previousToken is TokenType.objectEnd)
                    {
                        // Assume that only white space is left in the file
                        break;
                    }
                    else
                    {
                        throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                    }
                }

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
                            else if (previousToken is TokenType.value)
                            {
                                ChangePreviousTokenType(TokenType.key);
                                previousToken = TokenType.assignment;
                            }
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
                    case var tokenBeginning when char.IsLetter(tokenBeginning):
                        {
                            if (previousToken is TokenType.unknown or TokenType.objectStart or TokenType.value or TokenType.objectEnd)
                                AddIncompleteToken(TokenType.key, tokenTrim);
                            else if (previousToken is TokenType.assignment)
                                AddIncompleteToken(TokenType.value, tokenTrim);
                            else if (previousToken is TokenType.value)
                            {
                                ChangePreviousTokenType(TokenType.key);
                                AddIncompleteToken(TokenType.value, tokenTrim);
                            }
                            else if (previousToken is TokenType.key)
                            {
                                ChangePreviousTokenType(TokenType.value);
                                AddIncompleteToken(TokenType.key, tokenTrim);
                            }
                            else
                                throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                            break;
                        }
                    case var tokenBeginning when char.IsDigit(tokenBeginning):
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
                        throw new ArgumentException($"Unexpected token '{token}' encountered in {new string(leftContent.Take(100).ToArray())}");
                }

                if (modifier is not null)
                {
                    incompleteTokens.Last().modifier = modifier;
                    modifier = null;
                }
            }
            catch (Exception e)
            {
                Debugger.Break();
                throw;
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
                        try
                        {
                            var oldToken = completeTokens.Pop();
                            lastToken = oldToken;
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
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

        completeToken[ValuesKey] = token.ArrayValues.Cast<object>().Concat(token.ArrayObjects.Select(n => GetCompleteTokens(n))).ToArray();

        return completeToken;
    }

    public static Dictionary<string, object[]> Read(string fileContent)
    {
        try
        {
            var incompleteTokens = GetListOfTokens(fileContent);
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
