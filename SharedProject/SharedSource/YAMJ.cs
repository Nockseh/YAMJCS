using System.Xml.Linq;

namespace YAMJCS;

internal static class YAMJ {
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string MudraptorJobId = "PlayerMudraptorJob";

    public static bool IsMudraptor(Character? character) {
        if (character is null) {
            return false;
        }

        var species = character.SpeciesName.Value;
        return string.Equals(species, PlayerRaptorSpecies) || string.Equals(species, PlayerRaptorHuskSpecies);
    }

    public static bool IsMudraptorJob(Character? character) {
        var jobId = character?.Info?.Job?.Prefab?.Identifier.Value;
        return string.Equals(jobId, MudraptorJobId);
    }

    public static void HandleCharacterCreated(Character? character) {
        Log("YAMJ.HandleCharacterCreated()"); //todo
        //wait 1 second
        CoroutineManager.Invoke(() => {
            try {
                if (!IsMudraptorJob(character)) {
                    Log("character is not mudraptor, aborting"); //todo
                    return;
                }
                if (character is null || character.Removed) {
                    Log("character doesnt exist, aborting"); //todo
                    return;
                }

                if (character.Info is null || character.Info.Job is null) {
                    Log("char doesnt have valid info"); //todo
                    return;
                }

                if (character.IsDead) {
                    Log("char is dead"); //todo
                    return;
                }

                var species = character.SpeciesName.Value;

                if (string.Equals(species, "Human", StringComparison.OrdinalIgnoreCase)) {
                    Log($"Respawning {character.Name} as {PlayerRaptorSpecies}");
                    RespawnCharacter(character, PlayerRaptorSpecies);
                    return;
                }

                if (IsMudraptor(character)) {
                    character.TeamID = CharacterTeamType.Team1;
                    AddToCrew(character);
                }
            }
            catch (Exception ex) {
                Log($"HandleCharacterCreated failed: {ex}");
            }
        }, 1.0f);
    }

    public static void AddToCrew(Character character) {
        try {
            GameMain.GameSession?.CrewManager?.AddCharacter(character);
        }
        catch (Exception ex) {
            Log($"AddToCrew failed for {character?.Name}: {ex}");
        }
    }

    public static void RemoveFromCrew(Character character) {
        try {
            if (character.Info is not null) {
                GameMain.GameSession?.CrewManager?.RemoveCharacterInfo(character.Info);
                GameMain.GameSession?.CrewManager?.RemoveCharacter(character, removeInfo: false,
                    resetCrewListIndex: true);
            }
            else {
                GameMain.GameSession?.CrewManager?.RemoveCharacter(character, removeInfo: false,
                    resetCrewListIndex: true);
            }
        }
        catch (Exception ex) {
            Log($"RemoveFromCrew failed for {character?.Name}: {ex}");
        }
    }

    public static void RespawnCharacter(Character oldCharacter, string speciesName,
        Action<Character>? callback = null) {
        Entity.Spawner.AddCharacterToSpawnQueue(speciesName, oldCharacter.WorldPosition, newCharacter => {
            try {
                var oldInfo = oldCharacter.Info;
                var owningClient = ReflectionUtil.FindOwningClient(oldCharacter);

                newCharacter.TeamID = oldCharacter.TeamID;

                if (oldInfo is not null) {
                    newCharacter.Info = CopyCharacterInfo(speciesName, oldInfo);
                }

                TransferInventory(oldCharacter, newCharacter);
                TransferTalents(oldCharacter, newCharacter);
                TransferSkills(oldCharacter, newCharacter);
                TransferAfflictions(oldCharacter, newCharacter);

                ReflectionUtil.TrySetClientCharacter(owningClient, newCharacter);

                AddToCrew(newCharacter);

                Log($"Respawned {oldCharacter.Name} as {speciesName}");

                if (GameMain.NetworkMember is null || GameMain.NetworkMember.IsClient) {
                    oldCharacter.Remove();
                }
                else {
                    Entity.Spawner.AddEntityToRemoveQueue(oldCharacter);
                }

                callback?.Invoke(newCharacter);
            }
            catch (Exception ex) {
                Log($"RespawnCharacter failed for {oldCharacter?.Name}: {ex}");
            }
        });
    }

    private static CharacterInfo CopyCharacterInfo(string speciesName, CharacterInfo source) {
        var target = new CharacterInfo(
            speciesName.ToIdentifier(),
            source.Name,
            source.OriginalName,
            source.Job
        );

        target.Salary = source.Salary;
        target.SetExperience(source.ExperiencePoints);
        target.PermanentlyDead = source.PermanentlyDead;
        target.RenamingEnabled = source.RenamingEnabled;
        target.TeamID = source.TeamID;
        target.InventoryData = source.InventoryData;
        target.HealthData = source.HealthData;
        target.OrderData = source.OrderData;
        target.StartItemsGiven = source.StartItemsGiven;
        target.HumanPrefabIds = source.HumanPrefabIds;
        target.OmitJobInMenus = source.OmitJobInMenus;
        target.AdditionalTalentPoints = source.AdditionalTalentPoints;
        target.TalentRefundPoints = source.TalentRefundPoints;
        target.TalentResetCount = source.TalentResetCount;
        target.MinReputationToHire = source.MinReputationToHire;

        if (source.Head is not null) {
            target.Head = source.Head;
        }

        foreach (var talent in source.UnlockedTalents) {
            target.UnlockedTalents.Add(talent);
        }

        foreach (var talent in source.ResettableExtraTalents) {
            target.ResettableExtraTalents.Add(talent);
        }

        return target;
    }

    private static void TransferInventory(Character oldCharacter, Character newCharacter) {
        if (oldCharacter.Inventory is null || newCharacter.Inventory is null) {
            return;
        }

        var limbSlots = new[] {
            InvSlotType.Card,
            InvSlotType.Headset,
            InvSlotType.Head,
            InvSlotType.LeftHand,
            InvSlotType.RightHand,
            InvSlotType.OuterClothes,
            InvSlotType.InnerClothes,
            InvSlotType.Bag
        };

        foreach (var slot in limbSlots) {
            try {
                var item = oldCharacter.Inventory.GetItemInLimbSlot(slot);
                if (item is null) {
                    continue;
                }

                item.Drop(oldCharacter);
                int index = newCharacter.Inventory.FindLimbSlot(slot);
                if (index >= 0) {
                    ReflectionUtil.TryInvokeBestPutItem(newCharacter.Inventory, item, index, oldCharacter);
                }
            }
            catch (Exception ex) {
                Log($"TransferInventory limb slot {slot} failed: {ex}");
            }
        }

        try {
            foreach (var item in oldCharacter.Inventory.AllItemsMod) {
                if (item is null || item.Removed) {
                    continue;
                }

                item.Drop(oldCharacter);

                if (!ReflectionUtil.TryPutItemAnywhere(newCharacter, item)) {
                    Log($"Could not move item {item.Prefab?.Identifier.Value} to {newCharacter.Name}");
                }
            }
        }
        catch (Exception ex) {
            Log($"TransferInventory general pass failed: {ex}");
        }
    }

    private static void TransferTalents(Character oldCharacter, Character newCharacter) {
        try {
            foreach (var talent in oldCharacter.CharacterTalents) {
                newCharacter.GiveTalent(talent.Prefab, true);
            }
        }
        catch (Exception ex) {
            Log($"TransferTalents failed: {ex}");
        }
    }

    private static void TransferSkills(Character oldCharacter, Character newCharacter) {
        try {
            var jobPrefab = oldCharacter.Info?.Job?.Prefab;
            if (jobPrefab is null || newCharacter.Info is null) { return; }

            foreach (var skill in jobPrefab.Skills) {
                try {
                    float level = oldCharacter.GetSkillLevel(skill.Identifier);
                    if (level > 0f) {
                        newCharacter.Info.SetSkillLevel(skill.Identifier, level, false);
                    }
                }
                catch {
                    // keep going
                }
            }
        }
        catch (Exception ex)
        {
            Log($"TransferSkills failed: {ex}");
        }
    }

    private static void TransferAfflictions(Character oldCharacter, Character newCharacter) {
        try {
            XElement node = new XElement("root");
            oldCharacter.CharacterHealth.Save(node);
            newCharacter.CharacterHealth.Load(node);
        }
        catch (Exception ex) {
            Log($"TransferAfflictions failed: {ex}");
        }
    }

    public static void Log(string message) {
        Plugin.Instance?.LoggerService?.Log($"[YAMJCS] {message}");
    }
}