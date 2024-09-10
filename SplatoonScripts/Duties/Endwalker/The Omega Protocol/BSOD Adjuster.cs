﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Colors;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Duties.Endwalker.The_Omega_Protocol;
internal class BSOD_Adjuster :SplatoonScript
{
    public override HashSet<uint> ValidTerritories => new() { 1122 };
    public override Metadata? Metadata => new(1, "Redmoon");

    public class CastID
    {
        public const uint StackMarker = 22393u;
        public const uint Ion = 31560u;
        public const uint StackCannon = 31615u;
        public const uint BSOD = 31611u;
    }

    private Config Conf => Controller.GetConfig<Config>();

    public class Config :IEzConfig
    {
        public List<string[]> LeftRightPriorities = new();
    }

    private const bool _debug = false;

    private List<IPlayerCharacter> _sortedList = new();
    private List<IPlayerCharacter> _stackedList = new();
    private bool _mechanicActive = false;

    public override void OnSettingsDraw()
    {
        ImGui.Text("# How to determine left/right priority : ");
        ImGui.SameLine();
        if (ImGui.SmallButton("Test"))
        {
            if (TryGetPriorityList(out var list))
            {
                DuoLog.Information($"Success: priority list {list.Print()}");
            }
            else
            {
                DuoLog.Warning($"Could not get priority list");
            }
        }
        var toRem = -1;
        for (int i = 0; i < Conf.LeftRightPriorities.Count; i++)
        {
            if (DrawPrioList(i))
            {
                toRem = i;
            }
        }
        if (toRem != -1)
        {
            Conf.LeftRightPriorities.RemoveAt(toRem);
        }
        if (ImGui.Button("Create new priority list"))
        {
            Conf.LeftRightPriorities.Add(new string[] { "", "", "", "", "", "", "", "" });
        }

        if (ImGui.CollapsingHeader("Debug"))
        {
            if (ImGui.Button("test"))
            {
                new TimedMiddleOverlayWindow("swaponYOU", 5000, () =>
                {
                    ImGui.SetWindowFontScale(2f);
                    ImGuiEx.Text(ImGuiColors.DalamudRed, $"Stack swap position!\n\n  Player 1 \n  Player 2");
                }, 150);
            }
        }
    }

    public override void OnSetup()
    {
        Controller.RegisterElementFromCode("StackLeft", "{\"Name\":\"\",\"refX\":94.5,\"refY\":111.5,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":5.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("StackRight", "{\"Name\":\"\",\"refX\":104.0,\"refY\":111.5,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":5.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if (source.GetObject() is IBattleNpc npc)
        {
            if (castId == CastID.Ion)
            {
                if (!TryGetPriorityList(out _sortedList))
                    return;
                _mechanicActive = true;
            }
            else if (castId == CastID.BSOD)
            {
                this.OnReset();
            }
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (!_mechanicActive || set.Action == null)
            return;

        if (set.Action.RowId == CastID.StackMarker)
        {
            if (set.Target is not IPlayerCharacter pcObj)
                return;

            _stackedList.Add(pcObj);
            if (_stackedList.Count == 2)
            {
                if (_sortedList.Exists(x => x.Name == Svc.ClientState.LocalPlayer.Name))
                {
                    // stacker show element
                    IPlayerCharacter myStacker = _stackedList.Where(x => x.Address == Svc.ClientState.LocalPlayer.Address).First();
                    IPlayerCharacter otherStacker = _stackedList.Where(x => x.Address != Svc.ClientState.LocalPlayer.Address).First();
                    int myIndex = _sortedList.IndexOf(myStacker);
                    int otherIndex = _sortedList.IndexOf(otherStacker);
                    if (myIndex == -1 || otherIndex == -1)
                    {
                        DuoLog.Warning($"Could not find player in priority list");
                        _mechanicActive = false;
                        this.OnReset();
                        return;
                    }
                    string myPos = myIndex < otherIndex ? "Left" : "Right";
                    string otherPos = myIndex < otherIndex ? "Right" : "Left";

                    Controller.GetElementByName($"Stack{myPos}").Enabled = true;
                    Controller.GetElementByName($"Stack{myPos}").tether = true;

                    DebugLog($"myStacker: {myStacker.Name} {myPos}, otherStacker: {otherStacker.Name} {otherPos}");
                    List<IPlayerCharacter> noneStackers = _sortedList.Where(x => !_stackedList.Contains(x)).ToList();
                    foreach (var x in noneStackers)
                    {
                        var dpos = noneStackers.IndexOf(x) < 3 ? "Left" : "Right";
                        DebugLog($"noneStacker: {x.Name} {dpos}");
                    }
                }
                else
                {
                    // non stacker show element
                    List<IPlayerCharacter> noneStackers = _sortedList.Where(x => !_stackedList.Contains(x)).ToList();
                    int myIndex = noneStackers.IndexOf(Svc.ClientState.LocalPlayer);
                    if (myIndex == -1)
                    {
                        DuoLog.Warning($"Could not find player in priority list");
                        _mechanicActive = false;
                        this.OnReset();
                        return;
                    }
                    string myPos = myIndex < 3 ? "Left" : "Right";

                    Controller.GetElementByName($"Stack{myPos}").Enabled = true;
                    Controller.GetElementByName($"Stack{myPos}").tether = true;

                    //Debug
                    IPlayerCharacter myStacker = _stackedList.First();
                    IPlayerCharacter otherStacker = _stackedList.Last();
                    myIndex = _sortedList.IndexOf(myStacker);
                    int otherIndex = _sortedList.IndexOf(otherStacker);
                    if (myIndex == -1 || otherIndex == -1)
                    {
                        DuoLog.Warning($"Could not find player in priority list");
                        _mechanicActive = false;
                        this.OnReset();
                        return;
                    }
                    myPos = myIndex < otherIndex ? "Left" : "Right";
                    string otherPos = myIndex < otherIndex ? "Right" : "Left";
                    DebugLog($"FirstStacker: {myStacker.Name} {myPos}, LastStacker: {otherStacker.Name} {otherPos}");

                    foreach (var x in noneStackers)
                    {
                        var dpos = noneStackers.IndexOf(x) < 3 ? "Left" : "Right";
                        DebugLog($"noneStacker: {x.Name} {dpos}");
                    }
                }
            }
            return;
        }

        if (set.Action.RowId == CastID.StackCannon)
        {
            Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
            _stackedList.Clear();
            DebugLog("===============================");
            return;
        }
    }

    public override void OnReset()
    {
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        _stackedList.Clear();
        _mechanicActive = false;
    }

    private bool TryGetPriorityList([NotNullWhen(true)] out List<IPlayerCharacter> values)
    {
        foreach (var p in Conf.LeftRightPriorities)
        {
            var valid = true;
            var l = FakeParty.Get().Select(x => x.Name.ToString()).ToHashSet();
            foreach (var x in p)
            {
                if (!l.Remove(x))
                {
                    valid = false;
                    break;
                }
            }
            if (valid)
            {
                values = FakeParty.Get().ToList().OrderBy(x => Array.IndexOf(p, x.Name.ToString())).ToList();
                return true;
            }
        }
        values = new();
        return false;
    }

    private bool DrawPrioList(int num)
    {
        var prio = Conf.LeftRightPriorities[num];
        ImGuiEx.Text($"# Priority list {num + 1}");
        ImGui.PushID($"prio{num}");
        ImGuiEx.Text($"    Omega Female");
        for (int i = 0; i < prio.Length; i++)
        {
            ImGui.PushID($"prio{num}element{i}");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText($"Player {i + 1}", ref prio[i], 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo("##partysel", "Select from party"))
            {
                foreach (var x in FakeParty.Get())
                {
                    if (ImGui.Selectable(x.Name.ToString()))
                    {
                        prio[i] = x.Name.ToString();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.PopID();
        }
        ImGuiEx.Text($"    Omega Male");
        if (ImGui.Button("Delete this list (ctrl+click)") && ImGui.GetIO().KeyCtrl)
        {
            return true;
        }
        ImGui.PopID();
        return false;
    }

    private void DebugLog(string log)
    {
        if (_debug)
            DuoLog.Information(log);
    }
}