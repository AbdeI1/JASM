﻿#nullable enable
using System.Diagnostics;
using System.Reflection;
using FuzzySharp;
using GIMI_ModManager.Core.Entities;
using Newtonsoft.Json;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class GenshinService : IGenshinService
{
    private readonly ILogger? _logger;

    private List<GenshinCharacter> _characters = new();

    private string _assetsUriPath = string.Empty;

    public GenshinService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<GenshinService>();
    }

    public async Task InitializeAsync(string assetsUriPath)
    {
        _logger?.Debug("Initializing GenshinService");
        var uri = new Uri(Path.Combine(assetsUriPath, "characters.json"));
        _assetsUriPath = assetsUriPath;
        var json = await File.ReadAllTextAsync(uri.LocalPath);


        var characters = JsonConvert.DeserializeObject<List<GenshinCharacter>>(json);
        //var characters = new[] { characterss };
        if (characters == null || !characters.Any())
        {
            _logger?.Error("Failed to deserialize GenshinCharacter list");
            return;
        }

        foreach (var character in characters)
        {
            SetImageUriForCharacter(assetsUriPath, character);
        }

        _characters.AddRange(characters);
        _characters.Add(getGlidersCharacter(assetsUriPath));
        _characters.Add(getOthersCharacter(assetsUriPath));
    }

    private static void SetImageUriForCharacter(string assetsUriPath, GenshinCharacter character)
    {
        if (character.ImageUri is not null && character.ImageUri.StartsWith("Character_"))
        {
            character.ImageUri = $"{assetsUriPath}/Images/{character.ImageUri}";
        }
    }

    public IEnumerable<GenshinCharacter> GetCharacters()
    {
        return _characters;
    }

    public GenshinCharacter? GetCharacter(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70)
    {
        var searchResult = new Dictionary<GenshinCharacter, int>();

        foreach (var character in restrictToGenshinCharacters ?? _characters)
        {
            var result = Fuzz.PartialRatio(keywords, character.DisplayName);

            if (keywords.Contains(character.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                keywords.Contains(character.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
                return character;

            if (keywords.ToLower().Split().Any(modKeyWord =>
                    character.Keys.Any(characterKeyWord => characterKeyWord.ToLower() == modKeyWord)))
                return character;

            if (result == 100)
            {
                return character;
            }

            if (result > fuzzRatio)
            {
                searchResult.Add(character, result);
            }
        }

        return searchResult.Any() ? searchResult.MaxBy(s => s.Value).Key : null;
    }

    public Dictionary<GenshinCharacter, int> GetCharacters(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70)
    {
        var searchResult = new Dictionary<GenshinCharacter, int>();

        foreach (var character in restrictToGenshinCharacters ?? _characters)
        {
            Debug.Assert(searchResult.Count(x => x.Value == 100) <= 1,
                "searchResult.Count(x => x.Value == 100) <= 1, Multiple 100 results");

            var result = Fuzz.Ratio(keywords.ToLower(), character.DisplayName.ToLower());


            if (keywords.Contains(character.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                keywords.Contains(character.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                searchResult.Add(character, 100);
                continue;
            }

            if (keywords.ToLower().Split().Any(modKeyWord =>
                    character.Keys.Any(characterKeyWord => characterKeyWord.ToLower() == modKeyWord)))
            {
                searchResult.Add(character, 100);
                continue;
            }


            if (result > fuzzRatio)
            {
                searchResult.Add(character, result);
            }
        }

        return searchResult;
    }


    private const int _otherCharacterId = -1234;
    public int OtherCharacterId => _otherCharacterId;

    private static GenshinCharacter getOthersCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _otherCharacterId,
            DisplayName = "Others",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "others", "unknown" },
            ImageUri = "Character_Others.png",
            Element = string.Empty,
            Weapon = string.Empty,
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    private const int _glidersCharacterId = -1235;
    public int GlidersCharacterId => _glidersCharacterId;

    private static GenshinCharacter getGlidersCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _glidersCharacterId,
            DisplayName = "Gliders",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "gliders", "glider", "wings" },
            ImageUri = "Character_Gliders_Thumb.webp"
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    public GenshinCharacter? GetCharacter(int id)
        => _characters.FirstOrDefault(c => c.Id == id);
}

public interface IGenshinService
{
    public Task InitializeAsync(string jsonFile);
    public IEnumerable<GenshinCharacter> GetCharacters();

    public GenshinCharacter? GetCharacter(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70);

    public Dictionary<GenshinCharacter, int> GetCharacters(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70);

    public GenshinCharacter? GetCharacter(int id);
    public int OtherCharacterId { get; }
    public int GlidersCharacterId { get; }
}

internal static class GenshinCharacters
{
    internal static IEnumerable<GenshinCharacter> AllCharacters() =>
        typeof(GenshinCharacters).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(GenshinCharacter))
            .Select(f => (GenshinCharacter)f.GetValue(null)!);


    internal static readonly GenshinCharacter Amber = new GenshinCharacter
    {
        DisplayName = "Amber",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 4,
        Element = "Pyro",
        Weapon = "Bow",
        Region = new[] { "Mondstadt" }
    };

    internal static readonly GenshinCharacter Barbara = new GenshinCharacter
    {
        DisplayName = "Barbara",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 4,
        Element = "Hydro",
        Weapon = "Catalyst",
        Region = new[] { "Mondstadt" }
    };

    internal static readonly GenshinCharacter Deluc = new GenshinCharacter
    {
        DisplayName = "Deluc",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 5,
        Element = "Pyro",
        Weapon = "Claymore",
        Region = new[] { "Mondstadt" }
    };
}