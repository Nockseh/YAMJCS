namespace YAMJCS;

internal static class RaptorGut {
    private static readonly Identifier UnidentifiedGeneTag = "unidentifiedgeneticmaterial".ToIdentifier();
    private static readonly Identifier GeneTag = "geneticmaterial".ToIdentifier();
    private static readonly Identifier DepletedFuelTag = "depletedfuel".ToIdentifier();

    private static ItemPrefab[]? _geneCache;
    
    internal static Item? FindShrapnelAmmo(CharacterInventory inventory) {
        for (int i = 0; i < inventory.Capacity; i++) {
            if (!RaptorInventory.IsGutSlot(inventory, i)) { continue; }

            Item? item = inventory.GetItemAt(i);
            if (item is null || item.Removed || item.IsInRemoveQueue) { continue; }
            if (item.HasTag(YAMJ.ShrapnelAmmo)) { return item; }
        }
        return null;
    }
    
    // for some reason afflictions are multiplied by maxVitality on application so i have to undo that for a clean 0% -> 100%
    private static void AddStrength(CharacterHealth charHealth, AfflictionPrefab? prefab, float amount) {
        if (prefab is null || amount <= 0f) { return; }
        Affliction? existing = charHealth.GetAffliction(prefab.Identifier, false);
        if (existing is not null) {
            existing.Strength += amount;
            charHealth.RecalculateVitality();
            return;
        }

        float vitalityScale = charHealth.MaxVitality / 100f;
        if (vitalityScale <= 0f) { vitalityScale = 1f; }
        charHealth.AddAffliction(prefab.Instantiate(amount * vitalityScale));
    }
    internal static void ModifyHunger(CharacterHealth charHealth, float amount) {
        if (amount > 0f) {
            //amount = hunger added
            float currentSatiety = charHealth.GetAfflictionStrengthByIdentifier(YAMJ.SatiatedAfflictionId, false);
            float satietyReduced = Math.Min(amount, currentSatiety);
            if (satietyReduced > 0f) { charHealth.ReduceAfflictionOnAllLimbs(YAMJ.SatiatedAfflictionId, satietyReduced); }
            AddStrength(charHealth, YAMJ.HungerPrefab, amount - satietyReduced);
            return;
        }
        else {
            //amount = satiety added
            amount = -amount;
            float currentHunger = charHealth.GetAfflictionStrengthByIdentifier(YAMJ.HungerAfflictionId, false);
            float hungerReduced = Math.Min(amount, currentHunger);
            if (hungerReduced > 0f) {
                charHealth.ReduceAfflictionOnAllLimbs(YAMJ.HungerAfflictionId, hungerReduced);
            }
            AddStrength(charHealth, YAMJ.SatiatedPrefab, amount - hungerReduced);
            return;
        }
    }
    
    private static ItemPrefab? GetRandomGene() {
        if (_geneCache is null) {
            List<ItemPrefab> genes = new List<ItemPrefab>();
            foreach (ItemPrefab prefab in ItemPrefab.Prefabs) {
                if (prefab.Tags.Contains(GeneTag)) { genes.Add(prefab); }
            }
            _geneCache = genes.ToArray();
        }
        if (_geneCache.Length == 0) { return null; }
        return _geneCache.GetRandomUnsynced();
    }

    private static void DigestIntoMaterials(Item item, CharacterInventory inventory) {
        ItemPrefab prefab = item.Prefab;
        List<DeconstructItem> outputs = new List<DeconstructItem>(prefab.DeconstructItems);

        if (prefab.RandomDeconstructionOutput) {
            List<int> indexes = new List<int>();
            List<float> weights = new List<float>();
            for (int i = 0; i < outputs.Count; i++) {
                indexes.Add(i);
                weights.Add(outputs[i].Commonness);
            }

            List<DeconstructItem> picked = new List<DeconstructItem>();
            for (int i = 0; i < prefab.RandomDeconstructionOutputAmount; i++) {
                if (indexes.Count < 1) { break; }
                int chosen = ToolBox.SelectWeightedRandom(indexes, weights, Rand.RandSync.Unsynced);
                int at = indexes.IndexOf(chosen);
                picked.Add(outputs[chosen]);
                indexes.RemoveAt(at);
                weights.RemoveAt(at);
            }
            outputs = picked;
        }

        Entity.Spawner?.AddItemToRemoveQueue(item);
        foreach (DeconstructItem output in outputs) {
            ItemPrefab outputPrefab = MapEntityPrefab.FindByIdentifier(output.ItemIdentifier) as ItemPrefab;
            if (outputPrefab is null) { continue; }
            for (int i = 0; i < output.Amount; i++) {
                Entity.Spawner?.AddItemToSpawnQueue(outputPrefab, inventory);
            }
        }
    }

    internal static void Digest(Character character, float deltaTime) {

        float hungerDelta = YAMJConfig.HungerRate;
        
        CharacterInventory inventory = character.Inventory;

        bool gutContainsOre = false;
        bool gutContainsMetal = false;
        
        // loop through gut inventory 
        for (int i = 0; i < inventory.Capacity; i++) {
            if (!RaptorInventory.IsGutSlot(inventory, i)) { continue; }

            Item item = inventory.GetItemAt(i);
            if (item is null)  { continue; }
            
            // Basic Food
            if (item.HasTag(YAMJ.BasicFood)) {
                hungerDelta -= YAMJConfig.BasicFoodHungerReduction;
                item.Condition  -= YAMJConfig.BasicFoodConditionReduction * deltaTime;
                if (item.Condition <= 0) {
                    Entity.Spawner?.AddItemToRemoveQueue(item);
                }
            }
            // Mechanic Food
            else if (item.HasTag(YAMJ.MechanicFood)) {
                gutContainsOre = true;
                hungerDelta -= YAMJConfig.MechanicFoodHungerReduction;
                item.Condition  -= YAMJConfig.MechanicFoodConditionReduction * deltaTime;
                if (item.Condition <= 0) {
                    DigestIntoMaterials(item, inventory);
                }
            }
            else if (item.HasTag(YAMJ.ShrapnelAmmo)) {
                gutContainsMetal = true;
            }
            // Engineer Food
            else if (item.HasTag(YAMJ.EngineerFood)) {
                hungerDelta -= YAMJConfig.EngineerFoodHungerReduction;
                item.Condition  -= YAMJConfig.EngineerFoodConditionReduction * deltaTime;
                //todo later: apply affliction
                if (item.Condition <= 0) {
                    Entity.Spawner?.AddItemToRemoveQueue(item);
                    ItemPrefab.Prefabs.TryGet(DepletedFuelTag, out ItemPrefab? prefab);
                    if (prefab is not null) {
                        Entity.Spawner?.AddItemToSpawnQueue(prefab, inventory);
                    }
                }
            }
            // Doctor Food
            else if (item.HasTag(YAMJ.DoctorFood)) {
                if (item.HasTag(UnidentifiedGeneTag)) {
                    hungerDelta -= YAMJConfig.DoctorFoodHungerReduction;
                    item.Condition += YAMJConfig.DoctorFoodConditionReduction * deltaTime;
                    if (item.Condition <= 0) {
                        Entity.Spawner?.AddItemToRemoveQueue(item);
                        Entity.Spawner?.AddItemToSpawnQueue(GetRandomGene(), inventory, 0.1f); //not percent, actual
                    }
                }
                else if (item.Condition < item.MaxCondition) { //already identified, but not fully refined
                    hungerDelta -= YAMJConfig.DoctorFoodHungerReduction;
                    item.Condition  -= YAMJConfig.DoctorFoodConditionReduction * deltaTime;
                }
            }
        }
        
        //apply other effects & modify hunger 
        if (gutContainsOre) {
            character.CharacterHealth.AddAffliction(YAMJ.OreInGutPrefab.Instantiate(1f));
        }
        else {
            character.CharacterHealth.ReduceAfflictionOnAllLimbs(YAMJ.OreInGutId, 2f);
        }
        
        if (gutContainsMetal) {
            character.CharacterHealth.AddAffliction(YAMJ.MetalInGutPrefab.Instantiate(1f));
        }
        else {
            character.CharacterHealth.ReduceAfflictionOnAllLimbs(YAMJ.MetalInGutId, 2f);
        }
        
        ModifyHunger(character.CharacterHealth, hungerDelta * deltaTime);
    }
}