namespace YAMJCS;

public static class RaptorGut {
    private static readonly Identifier UnidentifiedGeneTag = "unidentifiedgeneticmaterial".ToIdentifier();
    private static readonly Identifier GeneTag = "geneticmaterial".ToIdentifier();
    private static readonly Identifier DepletedFuelTag = "depletedfuel".ToIdentifier();
    
    private static ItemPrefab[] geneCache;

    private static ItemPrefab GetRandomGene() {
        if (geneCache is null) {
            List<ItemPrefab> genes = new List<ItemPrefab>();
            foreach (ItemPrefab prefab in ItemPrefab.Prefabs) {
                if (prefab.Tags.Contains(GeneTag)) { genes.Add(prefab); }
            }
            geneCache = genes.ToArray();
        }
        if (geneCache.Length == 0) { return null; }
        return geneCache.GetRandomUnsynced();
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

    public static void Digest(Character character, float deltaTime) {
        
        float hungerReduction = 0f;
        CharacterInventory inventory = character.Inventory;
        
        for (int i = 0; i < inventory.Capacity; i++) {
            if (!RaptorInventory.IsGutSlot(inventory, i)) { continue; }

            Item item = inventory.GetItemAt(i);
            
            // Basic Food
            if (item.HasTag(YAMJ.BasicFood)) {
                hungerReduction += YAMJConfig.BasicFoodHungerReduction;
                item.Condition  -= YAMJConfig.BasicFoodConditionReduction * deltaTime;
                if (item.Condition <= 0) {
                    Entity.Spawner?.AddItemToRemoveQueue(item);
                }
            }
            // Mechanic Food
            if (item.HasTag(YAMJ.MechanicFood)) {
                hungerReduction += YAMJConfig.MechanicFoodHungerReduction;
                item.Condition  -= YAMJConfig.MechanicFoodConditionReduction * deltaTime;
                //todo later: apply buff
                if (item.Condition <= 0) {
                    DigestIntoMaterials(item, inventory);
                }
            }
            // Engineer Food
            if (item.HasTag(YAMJ.EngineerFood)) {
                hungerReduction += YAMJConfig.EngineerFoodHungerReduction;
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
            if (item.HasTag(YAMJ.DoctorFood)) {
                if (item.HasTag(UnidentifiedGeneTag)) {
                    hungerReduction += YAMJConfig.DoctorFoodHungerReduction;
                    item.Condition += YAMJConfig.DoctorFoodConditionReduction * deltaTime;
                    if (item.Condition <= 0) {
                        Entity.Spawner?.AddItemToRemoveQueue(item);
                        Entity.Spawner?.AddItemToSpawnQueue(GetRandomGene(), inventory, 0.1f); //not percent, actual
                    }
                }
                else if (item.Condition < item.MaxCondition) { //already identified, but not fully refined
                    hungerReduction += YAMJConfig.DoctorFoodHungerReduction;
                    item.Condition  -= YAMJConfig.DoctorFoodConditionReduction * deltaTime;
                }
            }
        }
        
        character.CharacterHealth.ReduceAfflictionOnAllLimbs(YAMJ.HungerAfflictionId, hungerReduction * deltaTime);
    }
}