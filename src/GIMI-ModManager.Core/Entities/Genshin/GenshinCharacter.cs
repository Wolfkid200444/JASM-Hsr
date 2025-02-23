﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GIMI_ModManager.Core.Entities.Genshin;

public record GenshinCharacter : IGenshinCharacter, IEqualityComparer<GenshinCharacter>
{
    public int Id { get; set; } = -1;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Keys { get; set; } = Array.Empty<string>();
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public string? ImageUri { get; set; }
    public int Rarity { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public Elements Element { get; set; }

    public string Weapon { get; set; } = string.Empty;
    public string[] Region { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<ISubSkin> InGameSkins { get; set; } = Array.Empty<Skin>();

    public string GetInternalSkinName()
    {
        var internalName = InGameSkins.FirstOrDefault(skin => skin.DefaultSkin)?.Name;

        return internalName ??
               throw new InvalidOperationException("No default skin found for character " + DisplayName);
    }

    public bool Equals(GenshinCharacter? x, GenshinCharacter? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(GenshinCharacter obj)
    {
        return obj.Id;
    }

    public bool Equals(IGenshinCharacter? x, IGenshinCharacter? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(IGenshinCharacter obj)
    {
        return obj.Id;
    }
}

public record Skin : ISubSkin, IEqualityComparer<ISubSkin>
{
    private IGenshinCharacter _character = null!;

    public Skin(bool defaultSkin, string displayName, string name, string skinSuffix)
    {
        DefaultSkin = defaultSkin;
        DisplayName = displayName;
        Name = name;
        SkinSuffix = skinSuffix;
    }

    [JsonIgnore]
    public IGenshinCharacter Character
    {
        get => _character;
        set => _character = _character is null
            ? value
            : throw new InvalidOperationException("Character cannot be set twice");
    }

    public bool DefaultSkin { get; }

    // Example: Default/CN/Sea Breeze Dandelion
    public string DisplayName { get; }
    public string Name { get; }

    // jean__/jeancn__/jeansea__, used to detect mod type from files
    public string SkinSuffix { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ImageUri { get; set; }

    public bool Equals(ISubSkin? x, ISubSkin? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Character.Equals(y.Character) &&
               string.Equals(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase) && string.Equals(x.SkinSuffix,
                   y.SkinSuffix, StringComparison.CurrentCultureIgnoreCase);
    }

    public int GetHashCode(ISubSkin obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(obj.Character);
        hashCode.Add(obj.Name, StringComparer.CurrentCultureIgnoreCase);
        hashCode.Add(obj.SkinSuffix, StringComparer.CurrentCultureIgnoreCase);
        return hashCode.ToHashCode();
    }
}

public interface IGenshinCharacter : IEqualityComparer<GenshinCharacter>, IEqualityComparer<IGenshinCharacter>
{
    int Id { get; }
    public string DisplayName { get; }
    public string[] Keys { get; }
    public DateTime ReleaseDate { get; }
    public string? ImageUri { get; }
    public int Rarity { get; }

    [JsonConverter(typeof(StringEnumConverter))]
    public Elements Element { get; }

    public string Weapon { get; }
    public string[] Region { get; }
    public IReadOnlyCollection<ISubSkin> InGameSkins { get; }

    public string GetInternalSkinName();
}

public interface ISubSkin : IEqualityComparer<ISubSkin>
{
    public IGenshinCharacter Character { get; internal set; }
    public bool DefaultSkin { get; }

    // Example: Default/CN/Sea Breeze Dandelion
    public string DisplayName { get; }

    // jean__/jeancn__/jeansea__, used to detect mod type from files
    public string Name { get; }


    // __/cn__/sea__, used to detect mod type from files
    public string SkinSuffix { get; }

    // If null, use default image
    public string? ImageUri { get; internal set; }
}

public enum Elements
{
    None,
    Geo,
    Cryo,
    Dendro,
    Electro,
    Anemo,
    Hydro,
    Pyro
}