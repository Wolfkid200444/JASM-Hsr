﻿#nullable enable
using FuzzySharp;
using GIMI_ModManager.Core.Entities.Genshin;
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
            SetSubSkinConnections(character);
        }

        _characters.AddRange(characters);
        _characters.Add(getGlidersCharacter(assetsUriPath));
        _characters.Add(getOthersCharacter(assetsUriPath));
        _characters.Add(getWeaponsCharacter(assetsUriPath));
    }

    private static void SetImageUriForCharacter(string assetsUriPath, GenshinCharacter character)
    {
        if (character.ImageUri is not null && character.ImageUri.StartsWith("Character_"))
            character.ImageUri = $"{assetsUriPath}/Images/{character.ImageUri}";
        foreach (var characterInGameSkin in character.InGameSkins)
        {
            if (characterInGameSkin.DefaultSkin)
                continue;
            if (characterInGameSkin.ImageUri is not null && characterInGameSkin.ImageUri.StartsWith("Character_"))
                characterInGameSkin.ImageUri =
                    $"{assetsUriPath}/Images/AltCharacterSkins/{characterInGameSkin.ImageUri}";
        }
    }

    private static void SetSubSkinConnections(GenshinCharacter character)
    {
        foreach (var skin in character.InGameSkins) skin.Character = character;
    }

    public List<GenshinCharacter> GetCharacters()
    {
        return new List<GenshinCharacter>(_characters);
    }

    public List<Elements> GetElements()
    {
        return Enum.GetValues<Elements>().ToList();
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

            if (result == 100) return character;

            if (result > fuzzRatio) searchResult.Add(character, result);
        }

        return searchResult.Any() ? searchResult.MaxBy(s => s.Value).Key : null;
    }

    public Dictionary<GenshinCharacter, int> GetCharacters(string searchQuery,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int minScore = 100)
    {
        return SearchCharacters(searchQuery, restrictToGenshinCharacters ?? _characters, minScore);
    }

    public static Dictionary<GenshinCharacter, int> SearchCharacters(string searchQuery,
        IEnumerable<GenshinCharacter> characters, int minScore = 100)
    {
        var searchResult = new Dictionary<GenshinCharacter, int>();
        searchQuery = searchQuery.ToLower().Trim();

        foreach (var character in characters)
        {
            var loweredDisplayName = character.DisplayName.ToLower();

            var result = 0;

            // If the search query contains the display name, we give it a lot of points
            var sameChars = loweredDisplayName.Split().Count(searchQuery.Contains);
            result += sameChars * 50;


            // A character can have multiple keys, so we take the best one. The keys are only used to help with searching
            var bestKeyMatch = character.Keys.Max(key => Fuzz.Ratio(key, searchQuery));
            result += bestKeyMatch;

            if (character.Keys.Any(key => key.Equals(searchQuery, StringComparison.CurrentCultureIgnoreCase)))
                result += 100;


            var splitNames = loweredDisplayName.Split();
            var sameStartChars = 0;
            var bestResultOfNames = 0;
            // This loop will give points for each name that starts with the same chars as the search query
            foreach (var name in splitNames)
            {
                sameStartChars = 0;
                foreach (var @char in searchQuery)
                {
                    if (name.ElementAtOrDefault(sameStartChars) == default(char)) continue;

                    if (name[sameStartChars] != @char) continue;

                    sameStartChars++;
                    if (sameStartChars > bestResultOfNames)
                        bestResultOfNames = sameStartChars;
                }
            }

            result += sameStartChars * 5; // Give more points for same start chars

            result += loweredDisplayName.Split()
                .Max(name => Fuzz.PartialRatio(name, searchQuery)); // Do a partial ratio for each name

            if (result < minScore) continue;

            searchResult.Add(character, result);
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
            Element = Elements.None,
            Weapon = string.Empty
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

    private const int _weaponsCharacterId = -1236;
    public int WeaponsCharacterId => _weaponsCharacterId;

    private static GenshinCharacter getWeaponsCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _weaponsCharacterId,
            DisplayName = "Weapons",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "weapon", "claymore", "sword", "polearm", "catalyst", "bow" },
            ImageUri = "Character_Weapons_Thumb.webp"
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    public GenshinCharacter? GetCharacter(int id)
    {
        return _characters.FirstOrDefault(c => c.Id == id);
    }

    public bool IsMultiModCharacter(GenshinCharacter character)
    {
        return IsMultiModCharacter(character.Id);
    }

    public bool IsMultiModCharacter(int characterId)
    {
        return characterId == OtherCharacterId || characterId == GlidersCharacterId ||
               characterId == WeaponsCharacterId;
    }
}

public interface IGenshinService
{
    public Task InitializeAsync(string jsonFile);
    public List<GenshinCharacter> GetCharacters();
    public List<Elements> GetElements();

    public GenshinCharacter? GetCharacter(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70);

    public Dictionary<GenshinCharacter, int> GetCharacters(string searchQuery,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int minScore = 70);

    public GenshinCharacter? GetCharacter(int id);
    public int OtherCharacterId { get; }
    public int GlidersCharacterId { get; }
    public int WeaponsCharacterId { get; }

    public bool IsMultiModCharacter(GenshinCharacter character);
    public bool IsMultiModCharacter(int characterId);
}