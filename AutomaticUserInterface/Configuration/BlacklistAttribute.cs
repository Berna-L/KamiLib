﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using ImGuiNET;
using KamiLib.AutomaticUserInterface;
using KamiLib.Caching;
using KamiLib.Localization;
using KamiLib.Utilities;
using Lumina.Excel.GeneratedSheets;
using Action = System.Action;

namespace NoTankYou.Models.Attributes;

public class BlacklistAttribute : DrawableAttribute
{
    private class SearchResult
    {
        public uint TerritoryID { get; init; }
        private uint PlaceNameRow => LuminaCache<TerritoryType>.Instance.GetRow(TerritoryID)?.PlaceName.Row ?? 0;
        public string TerritoryName => LuminaCache<PlaceName>.Instance.GetRow(PlaceNameRow)?.Name.ToDalamudString().TextValue ?? "Unknown PlaceName Row";
    }
    
    private static string _searchString = string.Empty;
    private static List<SearchResult>? _searchResults = new();

    public BlacklistAttribute() : base(null)
    {
        _searchResults = Search("", 10);
    }
    
    protected override void Draw(object obj, MemberInfo field, Action? saveAction = null)
    {
        var hashSet = GetValue<HashSet<uint>>(obj, field);
        var regionSize = ImGui.GetContentRegionAvail();
        
        ImGui.SetCursorPos(ImGui.GetCursorPos() with { X = regionSize.X * 0.15f / 2.0f } );
        ImGui.PushItemWidth(regionSize.X * 0.85f);
        if (ImGui.InputTextWithHint("##SearchBox", Strings.Search, ref _searchString, 64, ImGuiInputTextFlags.AutoSelectAll))
        {
            _searchResults = Search(_searchString, 10);
        }

        DrawSearchResults(obj, field, saveAction, hashSet);

        ImGuiHelpers.ScaledDummy(5.0f);
        
        DrawCurrentlyBlacklisted(obj, field, saveAction, hashSet);
    }

    private void DrawSearchResults(object obj, MemberInfo field, Action? saveAction, IReadOnlySet<uint> hashSet)
    {
        var regionSize = ImGui.GetContentRegionAvail();
        var width = regionSize.X * 0.85f;
        var height = 270.0f * ImGuiHelpers.GlobalScale;
        
        ImGui.SetCursorPos(ImGui.GetCursorPos() with { X = regionSize.X * 0.15f / 2.0f } );
        if (ImGui.BeginChild("##SearchResults", new Vector2(width, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (_searchResults is not null)
            {
                foreach (var result in _searchResults)
                {
                    if (hashSet.Contains(result.TerritoryID))
                    {
                        if (ImGuiComponents.IconButton($"RemoveButton{result.TerritoryID}", FontAwesomeIcon.Trash))
                        {
                            RemoveZone(obj, field, saveAction, result.TerritoryID);
                        }
                    }
                    else
                    {
                        if (ImGuiComponents.IconButton($"AddButton{result.TerritoryID}", FontAwesomeIcon.Plus))
                        {
                            AddZone(obj, field, saveAction, result.TerritoryID);
                        }
                    }

                    ImGui.SameLine();

                    DrawTerritory(result.TerritoryID);
                }
            }
        }
        ImGui.EndChild();
    }
    
    private void DrawCurrentlyBlacklisted(object obj, MemberInfo field, Action? saveAction, HashSet<uint> hashSet)
    {
        var regionSize = ImGui.GetContentRegionAvail();
        var width = regionSize.X * 0.85f;
        var height = 270.0f * ImGuiHelpers.GlobalScale * 0.5f;
        var removalSet = new HashSet<uint>();

        ImGui.SetCursorPos(ImGui.GetCursorPos() with { X = regionSize.X * 0.15f / 2.0f } );
        if (ImGui.BeginChild("##CurrentlyBlacklisted", new Vector2(width, height), true))
        {
            if (hashSet.Count == 0)
            {
                ImGui.TextUnformatted(Strings.NothingBlacklisted);
            }

            foreach (var zone in hashSet)
            {
                if (ImGuiComponents.IconButton($"RemoveButton{zone}", FontAwesomeIcon.Trash))
                {
                    removalSet.Add(zone);
                }

                ImGui.SameLine();

                DrawTerritory(zone);
            }

            foreach (var removalZone in removalSet)
            {
                RemoveZone(obj, field, saveAction, removalZone);
            }
        }
        ImGui.EndChild();
    }

    private void AddZone(object obj, MemberInfo field, Action? saveAction, uint zone)
    {
        var hashSet = GetValue<HashSet<uint>>(obj, field);
        hashSet.Add(zone);
        saveAction?.Invoke();
    }
    
    private void RemoveZone(object obj, MemberInfo field, Action? saveAction, uint zone)
    {
        var hashSet = GetValue<HashSet<uint>>(obj, field);
        hashSet.Remove(zone);
        saveAction?.Invoke();
    }
    
    private void DrawTerritory(uint zoneId)
    {
        var zone = LuminaCache<TerritoryType>.Instance.GetRow(zoneId);
        if (zone is null) return;

        var placeNameKey = zone.PlaceName.Row;
        var placeNameStringRow = LuminaCache<PlaceName>.Instance.GetRow(placeNameKey)!;
        var territoryName = placeNameStringRow.Name.ToDalamudString().ToString();
        
        if (ImGui.BeginTable($"##TerritoryTypeTable{zone.RowId}", 2))
        {
            ImGui.TableSetupColumn($"##TerritoryRow{zone.RowId}", ImGuiTableColumnFlags.WidthFixed, 50.0f);
            ImGui.TableSetupColumn($"##Label{zone.RowId}");

            ImGui.TableNextColumn();
            ImGui.TextColored(KnownColor.Gray.AsVector4(), zone.RowId.ToString());
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(territoryName);
            
            ImGui.EndTable();
        }
    }
    
    private static List<SearchResult> Search(string searchTerms, int numResults)
    {
        return LuminaCache<TerritoryType>.Instance
            .Where(territory => territory.PlaceName.Row is not 0)
            .Where(territory => territory.PlaceName.Value is not null)
            .GroupBy(territory => territory.PlaceName.Value!.Name.ToDalamudString().TextValue)
            .Select(territory => territory.First())
            .Where(territory => territory.PlaceName.Value!.Name.ToDalamudString().TextValue.ToLower().Contains(searchTerms.ToLower()))
            .Select(territory => new SearchResult {
                TerritoryID = territory.RowId
            })
            .OrderBy(searchResult => searchResult.TerritoryName)
            .Take(numResults)
            .ToList();
    }
}
