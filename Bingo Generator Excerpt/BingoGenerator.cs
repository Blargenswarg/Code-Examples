using DotNext.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;

public struct HintDesc
{
    JObject HintDescJObject;

    public HintDesc(List<string> HintKeywords, int KWsToAdd, string KeyWordType, int AppendChance, int ExtraResultChance, int PriorityChance = 0, int ForcedDuplicateAmount = 0, bool NAForStarting = false, int inkCost = -1, int itemLotId = -1, int maxDistance = -1, string HintNameOverride = "", string OriginTypeName = "Origin Name", string originName = "")
    {
        HintDescJObject = new JObject();

        if(originName != "")
            HintDescJObject.Add(OriginTypeName, originName);

        JArray keywords = new JArray();
        foreach (string kw in HintKeywords)
            keywords.Add(kw);

        HintDescJObject.Add("Hint Keywords", keywords);
        HintDescJObject.Add("KWsToAdd", KWsToAdd);
        HintDescJObject.Add("KeyWordType", KeyWordType);
        HintDescJObject.Add("NAForStarting", NAForStarting);
        HintDescJObject.Add("appendChance", AppendChance);
        HintDescJObject.Add("extraResultChance", ExtraResultChance);
        HintDescJObject.Add("forcedDuplicateAmount", ForcedDuplicateAmount);
        if (PriorityChance != 0)
            HintDescJObject.Add("priorityChance", PriorityChance);
        if (inkCost != -1)
            HintDescJObject.Add("inkCost", inkCost);
        if (itemLotId != -1)
            HintDescJObject.Add("ItemLotID", itemLotId);
        if (maxDistance != -1)
            HintDescJObject.Add("maxDistance", maxDistance);
        if (HintNameOverride != "")
            HintDescJObject.Add("Hint Name Override", HintNameOverride);
    }

    public HintDesc(JObject jObject, string OriginTypeName = "Origin Name", string originName = "")
    {
        HintDescJObject = jObject;
        if(originName != "")
            HintDescJObject.Add(OriginTypeName, originName);
    }
    public JObject getJObject()
    {
        return HintDescJObject;
    }
    public override string ToString()
    {
        return HintDescJObject.ToString();
    }
}
public struct GoalCategory
{
    [ThreadStatic] public static int CategoryIDCounter = 3; // IDs 0, 1, and 2 are reserved for the None, Any and Special categories.

    public int ID { get; set; }
    public string Name { get; set; }
    public HashSet<string> ExclusivityBuddies { get; set; }
    public HashSet<int> ExclusivityBuddiesIDs { get; set; }
    public HashSet<string> CategoryBuddies { get; set; }
    public HashSet<string> Received1WCategoryBuddies { get; set; }
    public int Chance { get; set; }
    public bool SelfExclusive { get; set; }
    public bool CanReplaceAny { get; set; }
    public int MaxPerBoard { get; set; }
    public bool IsChallengeOnly { get; set; }
    public List<int> RequiredTiers { get; set; }

    public GoalCategory(string Name, int Chance = 30, bool IsChallengeOnly = false, bool SelfExclusive = false, bool CanReplaceAny = false, int ID = -1, int MaxPerBoard = -1)
    {
        this.Name = Name;
        this.Chance = Chance;
        this.SelfExclusive = SelfExclusive;
        this.CanReplaceAny = CanReplaceAny;
        this.IsChallengeOnly = IsChallengeOnly;
       
        if (MaxPerBoard == -1)
            this.MaxPerBoard = 25;
        else
            this.MaxPerBoard = MaxPerBoard;

        if (ID == -1) 
            this.ID = CategoryIDCounter++;
        else 
            this.ID = ID;

        ExclusivityBuddies = new HashSet<string>();
        ExclusivityBuddiesIDs = new HashSet<int>();
        CategoryBuddies = new HashSet<string>();
        Received1WCategoryBuddies = new HashSet<string>();
        RequiredTiers = new List<int>();
    }

    public void UpdateEBIDs(ref Dictionary<string, GoalCategory> Categories)
    {
        ExclusivityBuddiesIDs.Clear();

        foreach (var EB in ExclusivityBuddies)
            ExclusivityBuddiesIDs.Add(Categories[EB].ID);
    }
    public override string ToString() { return Name; }

    public static explicit operator    int(GoalCategory obj) { return obj.ID; }
    public static explicit operator string(GoalCategory obj) { return obj.Name; }
    public static bool operator ==(GoalCategory a, GoalCategory b) { return a.ID == b.ID; }
    public static bool operator !=(GoalCategory a, GoalCategory b) { return a.ID != b.ID; }
    public static bool operator ==(GoalCategory a, string b) { return a.Name == b; }
    public static bool operator !=(GoalCategory a, string b) { return a.Name != b; }
}
#if DEBUG
// Left here for tier grid baking
public enum GT // GT means GridTransform
{
    flipH,
    rotCW,
    permute
}
#endif
public static class MyExtensions
{
    public static void AddNew(this Dictionary<int, HashSet<int>>[] value, int LineIndex, int CatIndex, int IndexIndex)
    {
        if (!value[LineIndex].ContainsKey(CatIndex))
            value[LineIndex][CatIndex] = new HashSet<int>();

        value[LineIndex][CatIndex].Add(IndexIndex);
    }
}

namespace ER_MiscRandomizer
{
    public class BingoGenerator
    {
        List<Goal> FullBaseGoalList = new List<Goal>();
        List<ChallengeModifier> FullChallengeModifierList = new List<ChallengeModifier>();
        List<List<List<List<Goal>>>> SortedGoals = new List<List<List<List<Goal>>>>();
        List<List<List<List<Goal>>>> SortedGoalsLimited = new List<List<List<List<Goal>>>>();
        List<List<List<List<HashSet<GoalCategory>>>>> SortedGoalsCommonCategories = new List<List<List<List<HashSet<GoalCategory>>>>>();
        Dictionary<string, GoalCategory> Categories = new Dictionary<string, GoalCategory>();
        Dictionary<int, string> CategoryNamesByID = new Dictionary<int, string>();
        List<string> AnyCategories = new List<string>();
        Dictionary<string, double> AnyCategoriesChances = new Dictionary<string, double>();
        HashSet<int> ForbiddenAnyCategories = new HashSet<int>();
        //Dictionary<string, HashSet<int>> ExclusivityBuddiesTable = new Dictionary<string, HashSet<int>>();
        RandomNumberGenerator BingoRand = null;
        int TierIterationCounter = 0;
        Dictionary<string, JArray> BoardDescJArrays = new Dictionary<string, JArray>();
        public static bool DebugPrints = false;
        public static bool LastResetDebugPrints = false;
        public static bool NoEvil = true;
        public static bool NoHitless = true;
        public static bool NoCategoryLogic = false;
        public static bool NoTierLogic = false;
        public static bool UseFasterTierGridChecking = false;
        
        public List<int> PossibleDistances = new List<int>();
        List<string> AllNoRegionTags;
        public static int TiersPerLine = 15;
//#if DEBUG                         
        public List<GoalCategory> GeneratedCategories = new List<GoalCategory>(); // Currently only used for tallying
        public List<GoalCategory> FinalCategories = new List<GoalCategory>(); // Currently only used for tallying
        public List<List<Goal>> SlotsPossibleGoals = new List<List<Goal>>();  // Currently only used for tallying
        public uint BoardsGenerated;
        public static bool TallyMode = false;
        public float ElapsedTime;
//#endif
#if DEBUG
        private string LastResetReason = "";
#endif
        // Region data declarations
        RegionData RegionCaelid = new RegionData();
        RegionData RegionLimgrave = new RegionData();
        RegionData RegionLiurnia = new RegionData();
        RegionData RegionAltus = new RegionData();
        RegionData RegionMountaintops = new RegionData();
        RegionData RegionLandOfShadow = new RegionData();
        List<RegionData> AllRegionData;
        public int StartingArea = -1;
        public int LoSExitWaygate = -1;
        public List<bool> EnabledRegions = new List<bool>();
        public int EnabledBaseGameRegionAmount = 5;

        string FinalList;
        public List<Goal> MainList = new List<Goal>();

        public class RegionData
        {
            public string RegionName { get; set; }
            public int RegionID { get; set; }
            public int LegacyDungeonCount { get; set; }
            public int DungeonCount { get; set; }
            public int CavesCount { get; set; }
            public int CatacombCount { get; set; }
            public int HerosGraveCount { get; set; }
            public int EvergaolCount { get; set; }
            public int GreatRuneCount { get; set; }

            public int OverworldBossCount { get; set; }
            public int NightBossCount { get; set; }
            public int MinorErdBossCount { get; set; }
            public int RuinBossCount { get; set; }
            public int InvaderCount { get; set; }
        }

        public struct GridData
        {
            public int[,] Grid { get; set; }
            public int Score { get; set; }

            public static bool operator <(GridData a, GridData b) { return a.Score < b.Score; }
            public static bool operator >(GridData a, GridData b) { return a.Score > b.Score; }
            public static bool operator ==(GridData a, GridData b) { return a.Score == b.Score; }
            public static bool operator !=(GridData a, GridData b) { return a.Score != b.Score; }
        }

        public class Goal
        {
            public string GoalDesc { get; set; }
            public List<string> LocationLevels { get; set; }
            public List<GoalCategory> GoalCategories { get; set; }
            public string ChallengeGoal { get; set; }
            public int Tier { get; set; }
            public int TierMin { get; set; }
            public int TierMax { get; set; }
            public int TierChallengeBase { get; set; }

            // PossibleTiers:
            // For individual goals this is first used to store its defined tierRange, then it gets overriden with the actual sorted possible tiers.
            // For MainList, this instead represents which tiers are possible for a slot on the board that has had its category (and more) chosen.
            public HashSet<int> PossibleTiers { get; set; } 
            public int BonusProbability { get; set; }
            public int Amount { get; set; }
            public int Region { get; set; }
            public int AmountMin { get; set; }
            public int AmountMax { get; set; }
            public int AmountStep { get; set; }
            public int AmountPerTier { get; set; }
            public int AmountExtraTierBreakpoint { get; set; }
            public int ChallengeRange { get; set; }
            public int ChallengeTier { get; set; }
            public List<string> ForbiddenGoals { get; set; }
            public Dictionary<string, int> ModifierTierChanges { get; set; }
            public int ModifierChangedAmount { get; set; }
            public bool AssignedChallengeGoal { get; set; }
            public List<HintDesc> HintDescs { get; set; }
            public string BaseGoalDesc { get; }
            public string Identifier { get; }
            public string ChallengeIdentifier { get; set; }
            public int MinimumRegions { get; }
            public bool MinimumRegionsIncludeLoS { get; }

            public Goal(string goalDesc, List<string> locationLevels, List<GoalCategory> goalCategories, List<HintDesc> hintDescs, List<int> tierRange, List<string> ForbiddenGoals, Dictionary<string, int> ModifierTierChanges, string challengeGoal = "False", int challengeRange = -1, int tier = -1, int probability = -1, int region = -1, int amountMin = -1, int amountMax = -1, int amountStep = -1, int amountPerTier = -1, int amountExtraTierBreakpoint = -1, int MinimumRegions = -1, bool MinimumRegionsIncludeLoS = false)
            {
                this.GoalDesc = goalDesc;
                this.LocationLevels = locationLevels;
                this.BaseGoalDesc = goalDesc;
                this.Identifier = this.BaseGoalDesc + (locationLevels[0] == "None" ? "" : (" (" + locationLevels[0] + ")"));
                this.ChallengeIdentifier = ""; 
                this.GoalCategories = goalCategories; 
                this.ChallengeGoal = challengeGoal;
                this.Tier = tier;

                this.BonusProbability = probability;
                this.Region = region;

                this.HintDescs = hintDescs;

                if (tierRange != null && tierRange.Count > 0)
                {
                    tierRange.Sort();
                    this.PossibleTiers = new HashSet<int>(tierRange);
                    this.TierMin = tierRange[0];
                    this.TierMax = tierRange.Last();
                }
                else
                {
                    this.PossibleTiers = new HashSet<int>();
                    this.TierMin = this.Tier;
                    this.TierMax = this.Tier;
                }
                this.TierChallengeBase = -1;

                if (ForbiddenGoals != null && ForbiddenGoals.Count > 0)
                    this.ForbiddenGoals = ForbiddenGoals;
                else
                    this.ForbiddenGoals = new List<string>();

                if (ModifierTierChanges != null && ModifierTierChanges.Count > 0)
                    this.ModifierTierChanges = ModifierTierChanges;
                else
                    this.ModifierTierChanges = new Dictionary<string, int>();

                this.ModifierChangedAmount = 0;

                if (ChallengeGoal != "False")
                    this.ChallengeRange = challengeRange;
                this.AmountMin                  = amountMin;
                this.AmountMax                  = amountMax;
                this.AmountStep                 = amountStep;
                this.AmountPerTier              = amountPerTier;
                this.AmountExtraTierBreakpoint  = amountExtraTierBreakpoint;
                this.AssignedChallengeGoal      = false;
                this.MinimumRegions             = MinimumRegions;
                this.MinimumRegionsIncludeLoS   = MinimumRegionsIncludeLoS;
            }

            public Goal()
            {
                this.GoalDesc = "";
                this.LocationLevels = new List<string>();
                this.BaseGoalDesc = "";
                this.Identifier = "";
                this.ChallengeIdentifier = "";
                this.GoalCategories = new List<GoalCategory>();
                this.ChallengeGoal = "False";
                this.Tier = -1;

                this.TierMin = -1;
                this.TierMax = -1;
                this.TierChallengeBase = -1;

                this.BonusProbability = -1;
                this.Region = -1;

                this.HintDescs = new List<HintDesc>();
                this.PossibleTiers = new HashSet<int>();
                this.ForbiddenGoals = new List<string>();
                this.ModifierTierChanges = new Dictionary<string, int>();
                this.ModifierChangedAmount = 0;

                this.ChallengeRange             = -1;
                this.AmountMin                  = -1;
                this.AmountMax                  = -1;
                this.AmountStep                 = -1;
                this.AmountPerTier              = -1;
                this.AmountExtraTierBreakpoint  = -1;
                this.AssignedChallengeGoal      = false;
            }

            public Goal(Goal other)
            {
                this.GoalDesc = other.GoalDesc;
                this.LocationLevels = other.LocationLevels;
                this.BaseGoalDesc = other.GoalDesc;
                this.Identifier = other.Identifier;
                this.ChallengeIdentifier = other.ChallengeIdentifier;
                this.GoalCategories = other.GoalCategories;
                this.ChallengeGoal = other.ChallengeGoal;
                this.Tier = other.Tier;

                this.TierMin = other.TierMin;
                this.TierMax = other.TierMax;
                this.TierChallengeBase = other.TierChallengeBase;

                this.BonusProbability = other.BonusProbability;
                this.Region = other.Region;

                this.HintDescs = other.HintDescs;
                this.PossibleTiers = other.PossibleTiers;
                this.ForbiddenGoals = other.ForbiddenGoals;
                this.ModifierTierChanges = other.ModifierTierChanges;
                this.ModifierChangedAmount = other.ModifierChangedAmount;

                this.ChallengeRange = other.ChallengeRange;
                this.AmountMin                  = other.AmountMin;
                this.AmountMax                  = other.AmountMax;
                this.AmountStep                 = other.AmountStep;
                this.AmountPerTier              = other.AmountPerTier;
                this.AmountExtraTierBreakpoint  = other.AmountExtraTierBreakpoint;
                this.AssignedChallengeGoal = other.AssignedChallengeGoal;
            }
            public override string ToString()
            {
                return this.GoalDesc;
            }
        }

        public class ChallengeModifier
        {
            public string ModifierDesc { get; set; }
            public List<string> ExcludedBossPools { get; set; }
            public bool IsFinishGoal { get; set; }
            public bool EnemyChallengeEligible { get; set; }
            public int BaseTierAdd { get; set; }
            public int ChallengeTier { get; set; }
            public int BonusProbability { get; set; }
            public string SoloEligible { get; set; }
            public string IsWeaponCollection { get; set; }
            public List<string> ForbiddenGoals { get; set; }
            public Dictionary<string, int> ModifierTierChanges { get; set; }
            public List<HintDesc> HintDescs { get; set; }

            public ChallengeModifier(string modifierDesc, List<string> ExcludedBossPools, bool IsFinishGoal, bool EnemyChallengeEligible, int BaseTierAdd, int ChallengeTier, int probability, string SoloEligible, string IsWeaponCollection, List<string> ForbiddenGoals, Dictionary<string, int> ModifierTierChanges, List<HintDesc> HintDescs)
            {
                this.ModifierDesc = modifierDesc;
                this.ExcludedBossPools = ExcludedBossPools;
                this.IsFinishGoal = IsFinishGoal;
                this.EnemyChallengeEligible = EnemyChallengeEligible;
                this.BaseTierAdd = BaseTierAdd;
                this.ChallengeTier = ChallengeTier;
                this.BonusProbability = probability;
                this.SoloEligible = SoloEligible;
                this.IsWeaponCollection = IsWeaponCollection;
                this.ForbiddenGoals = ForbiddenGoals;
                this.ModifierTierChanges = ModifierTierChanges;
                this.HintDescs = HintDescs;
            }
        }

        public class ChallengeModifierFinal
        {
            public ChallengeModifier ChallengeModifier { get; set; }
            public bool IsSolo { get; set; }

            public ChallengeModifierFinal(ChallengeModifier ChallengeModifier, bool IsSolo)
            {
                this.ChallengeModifier = ChallengeModifier;
                this.IsSolo = IsSolo;
            }
        }

        public struct TallyData
        {
            public List<GoalCategory> GeneratedCategories;
            public List<GoalCategory> FinalCategories;
            public List<Goal> GoalList;
            public List<List<Goal>> SlotsPossibleGoals;
        }
        public struct CheckGridHashSets
        {
            public HashSet<int>[] rows = new HashSet<int>[5];
            public HashSet<int>[] cols = new HashSet<int>[5];
            public HashSet<int>[] diagonals = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };
            public HashSet<int>[] rowsEB = new HashSet<int>[5];
            public HashSet<int>[] colsEB = new HashSet<int>[5];
            public HashSet<int>[] diagonalsEB = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };

            public CheckGridHashSets()
            {
                for (int i = 0; i < 5; i++)
                {
                    rows[i] = new HashSet<int>();
                    cols[i] = new HashSet<int>();
                    rowsEB[i] = new HashSet<int>();
                    colsEB[i] = new HashSet<int>();
                }

            }
        }
        public struct CheckGridDictionaries
        {
            // These are all arrays of dictionaries. The index of the array is the index of the row/col/diag,
            // the key of the dictionary are category IDs, the value is the offending index into the row/col/diag.
            // As such, ContainsKey can be used to check if a category is in a row.
            public Dictionary<int, HashSet<int>>[] rows = new Dictionary<int, HashSet<int>>[5];
            public Dictionary<int, HashSet<int>>[] cols = new Dictionary<int, HashSet<int>>[5];
            public Dictionary<int, HashSet<int>>[] diagonals = new Dictionary<int, HashSet<int>>[] { new Dictionary<int, HashSet<int>>(), new Dictionary<int, HashSet<int>>() };
            public Dictionary<int, HashSet<int>>[] rowsEB = new Dictionary<int, HashSet<int>>[5];
            public Dictionary<int, HashSet<int>>[] colsEB = new Dictionary<int, HashSet<int>>[5];
            public Dictionary<int, HashSet<int>>[] diagonalsEB = new Dictionary<int, HashSet<int>>[] { new Dictionary<int, HashSet<int>>(), new Dictionary<int, HashSet<int>>() };

            public CheckGridDictionaries()
            {
                for (int i = 0; i < 5; i++)
                {
                    rows[i] = new Dictionary<int, HashSet<int>>();
                    rowsEB[i] = new Dictionary<int, HashSet<int>>();
                    cols[i] = new Dictionary<int, HashSet<int>>();
                    colsEB[i] = new Dictionary<int, HashSet<int>>();
                }
            }
        }
        public class CategoriesGridChecker
        {
            CheckGridHashSets CGHS = new CheckGridHashSets();
            const int SIZE = 5;

            // Checks a grid to see if the GoalCategories are exclusive to their row/column/diagonal. This version is used during the initial
            // category generation. Some categories have exclusivity buddies and can be non self exclusive, this function is where those
            // variables are relevant.
            public bool CheckGridCategory(GoalCategory[,] Grid, int Index)
            {
                int row = Index / SIZE;
                int col = Index % SIZE;

                if (Grid[row, col].Name == "Any")
                    return true;

                int currentCategoryID = Grid[row, col].ID;
                bool currentIsSE = Grid[row, col].SelfExclusive;
                
                // If the current category is self-exclusive, check if any relevant line contains the category.
                if (currentIsSE)
                {
                    if (CGHS.rows[row].Contains(currentCategoryID) || CGHS.cols[col].Contains(currentCategoryID) || (row == col && CGHS.diagonals[0].Contains(currentCategoryID)) || (row + col == 4 && CGHS.diagonals[1].Contains(currentCategoryID)))
                        return false;
                }

                // For current category, check if any relevant line has it as an exclusivity buddy.
                if (CGHS.rowsEB[row].Contains(currentCategoryID) || CGHS.colsEB[col].Contains(currentCategoryID) || (row == col && CGHS.diagonalsEB[0].Contains(currentCategoryID)) || (row + col == 4 && CGHS.diagonalsEB[1].Contains(currentCategoryID)))
                    return false;

                AddSlot(row, col, currentCategoryID, Grid[row, col].ExclusivityBuddiesIDs);

                return true;
            }

            public void AddSlot(int row, int col, int currentCategoryID, HashSet<int> ExclusivityBuddiesIDs)
            {
                CGHS.rows[row].Add(currentCategoryID);
                CGHS.cols[col].Add(currentCategoryID);
                if (row == col)
                {
                    CGHS.diagonals[0].Add(currentCategoryID);
                }
                if (row + col == 4)
                {
                    CGHS.diagonals[1].Add(currentCategoryID);
                }

                foreach (int val in ExclusivityBuddiesIDs)
                {
                    CGHS.rowsEB[row].Add(val);
                    CGHS.colsEB[col].Add(val);
                    if (row == col)
                    {
                        CGHS.diagonalsEB[0].Add(val);
                    }
                    if (row + col == 4)
                    {
                        CGHS.diagonalsEB[1].Add(val);
                    }
                }
            }

            public List<GoalCategory> GetAvailableCategoriesForCell(ref List<GoalCategory> categories, int Index)
            {
                int row = Index / SIZE;
                int col = Index % SIZE;

                var availableCategories = new List<GoalCategory>();

                foreach (var category in categories)
                {
                    if (category.Name == "Any")
                        continue;

                    int categoryID = category.ID;
                    bool isSelfExclusive = category.SelfExclusive;

                    if (isSelfExclusive)
                    {
                        if (CGHS.rows[row].Contains(categoryID) || CGHS.cols[col].Contains(categoryID) || (row == col && CGHS.diagonals[0].Contains(categoryID)) || (row + col == 4 && CGHS.diagonals[1].Contains(categoryID)))
                            continue;
                    }

                    // For current category, check if any relevant line has it as an exclusivity buddy.
                    if (CGHS.rowsEB[row].Contains(categoryID) || CGHS.colsEB[col].Contains(categoryID) || (row == col && CGHS.diagonalsEB[0].Contains(categoryID)) || (row + col == 4 && CGHS.diagonalsEB[1].Contains(categoryID)))
                        continue;

                    availableCategories.Add(category);
                }

                return availableCategories;
            }
            public List<int> GetAvailableCategoryChancesForCell(List<GoalCategory> categories, int Index)
            {
                int row = Index / SIZE;
                int col = Index % SIZE;

                var availableCategoriesChances = new List<int>();

                foreach (var category in categories)
                {
                    if (category.Name == "Any")
                        continue;

                    int categoryID = category.ID;
                    bool isSelfExclusive = category.SelfExclusive;

                    if (isSelfExclusive)
                    {
                        if (CGHS.rows[row].Contains(categoryID) || CGHS.cols[col].Contains(categoryID) || (row == col && CGHS.diagonals[0].Contains(categoryID)) || (row + col == 4 && CGHS.diagonals[1].Contains(categoryID)))
                            continue;
                    }

                    // For current category, check if any relevant line has it as an exclusivity buddy.
                    if (CGHS.rowsEB[row].Contains(categoryID) || CGHS.colsEB[col].Contains(categoryID) || (row == col && CGHS.diagonalsEB[0].Contains(categoryID)) || (row + col == 4 && CGHS.diagonalsEB[1].Contains(categoryID)))
                        continue;

                    availableCategoriesChances.Add(category.Chance);
                }

                return availableCategoriesChances;
            }
            public List<GoalCategory> GetAvailableAnyCategoriesForCell(ref List<String> AnyCategories, ref Dictionary<string, GoalCategory> AllCategories, ref HashSet<int> ForbiddenAnyCategories, int Index)
            {
                List<GoalCategory> RelevantCategories = new List<GoalCategory>();

                foreach (string Category in AnyCategories)
                {
                    if (ForbiddenAnyCategories.Contains(AnyCategories.IndexOf(Category)))
                        continue;

                    RelevantCategories.Add(AllCategories[Category]);
                }

                return GetAvailableCategoriesForCell(ref RelevantCategories, Index);
            }

            public List<int> GetAvailableAnyCategoriesChancesForCell(ref List<String> AnyCategories, ref Dictionary<string, GoalCategory> AllCategories, ref HashSet<int> ForbiddenAnyCategories, int Index)
            {
                List<GoalCategory> RelevantCategories = new List<GoalCategory>();

                foreach (string Category in AnyCategories)
                {
                    if (ForbiddenAnyCategories.Contains(AnyCategories.IndexOf(Category)))
                        continue;

                    RelevantCategories.Add(AllCategories[Category]);
                }

                return GetAvailableCategoryChancesForCell(RelevantCategories, Index);
            }
        }

        public class GoalsGridChecker
        {
            // These are all arrays of dictionaries. The index of the array is the index of the row/col/diag,
            // the key of the dictionary are category IDs, the value is the offending index into the row/col/diag.
            public Dictionary<int, int>[] rowsCategories = new Dictionary<int, int>[5];
            public Dictionary<int, int>[] colsCategories = new Dictionary<int, int>[5];
            public Dictionary<int, int>[] diagonalsCategories = new Dictionary<int, int>[] { new Dictionary<int, int>(), new Dictionary<int, int>() };
            public Dictionary<int, int>[] rowsEBCategories = new Dictionary<int, int>[5];
            public Dictionary<int, int>[] colsEBCategories = new Dictionary<int, int>[5];
            public Dictionary<int, int>[] diagonalsEBCategories = new Dictionary<int, int>[] { new Dictionary<int, int>(), new Dictionary<int, int>() };

            public GoalsGridChecker()
            {
                for (int i = 0; i < 5; i++)
                {
                    rowsCategories[i] = new Dictionary<int, int>();
                    rowsEBCategories[i] = new Dictionary<int, int>();
                    colsCategories[i] = new Dictionary<int, int>();
                    colsEBCategories[i] = new Dictionary<int, int>();
                }
            }

            // Checks a grid to see if the Goals are exclusive to their row/column/diagonal. This version of 'CheckGrid' is used when picking
            // a specific goal for each cell to see if it is valid. This version is unique in that it checks each category for every goal
            // instead of one. It is also meant to be called every time a new cell is added, index refers to which cell was added, the function
            // returns a problematicIndex which points to which cell conflicted with the currently checked cell.
            public bool CheckGridGoals(Goal[,] Grid, int index, ref int problematicIndex)
            {
                int Size = 5;

                int row = index / Size;
                int col = index % Size;

                HashSet<int> allCurrentCategories = new HashSet<int>();
                HashSet<int> currentSECategories = new HashSet<int>();
                HashSet<int> currentEBCategories = new HashSet<int>();

                foreach (var category in Grid[row, col].GoalCategories)
                {
                    allCurrentCategories.Add(category.ID);

                    if (category.SelfExclusive)
                        currentSECategories.Add(category.ID);

                    currentEBCategories.AddAll(category.ExclusivityBuddiesIDs);
                }

                // For each self-exclusive category of the current goal, check if any relevant line contains that category.
                foreach (var val in currentSECategories)
                {
                    if (rowsCategories[row].ContainsKey(val))
                    {
                        problematicIndex = row * 5 + rowsCategories[row][val];
                        return false;
                    }
                    else if (colsCategories[col].ContainsKey(val))
                    {
                        problematicIndex = colsCategories[col][val] * 5 + col;
                        return false;
                    }
                    else if (row == col && diagonalsCategories[0].ContainsKey(val))
                    {
                        problematicIndex = diagonalsCategories[0][val] * 5 + diagonalsCategories[0][val];
                        return false;
                    }
                    else if (row + col == 4 && diagonalsCategories[1].ContainsKey(val))
                    {
                        problematicIndex = diagonalsCategories[1][val] * 5 + (4 - diagonalsCategories[1][val]);
                        return false;
                    }
                }

                // For each and all current categories of the current goal, check if any relevant line has it as an exclusivity buddy.
                foreach (var val in allCurrentCategories)
                {
                    if (rowsEBCategories[row].ContainsKey(val))
                    {
                        problematicIndex = row * 5 + rowsEBCategories[row][val];
                        return false;
                    }
                    else if (colsEBCategories[col].ContainsKey(val))
                    {
                        problematicIndex = colsEBCategories[col][val] * 5 + col;
                        return false;
                    }
                    else if (row == col && diagonalsEBCategories[0].ContainsKey(val))
                    {
                        problematicIndex = diagonalsEBCategories[0][val] * 5 + diagonalsEBCategories[0][val];
                        return false;
                    }
                    else if (row + col == 4 && diagonalsEBCategories[1].ContainsKey(val))
                    {
                        problematicIndex = diagonalsEBCategories[1][val] * 5 + (4 - diagonalsEBCategories[1][val]);
                        return false;
                    }
                }

                // Add the values of the current goals to the dictionaries for future checks.
                // Then return true, the goal is valid.

                foreach (var val in allCurrentCategories)
                {
                    rowsCategories[row][val] = col;
                    colsCategories[col][val] = row;
                    if (row == col)
                    {
                        diagonalsCategories[0][val] = row;
                    }
                    if (row + col == 4)
                    {
                        diagonalsCategories[1][val] = row;
                    }
                }

                foreach (var val in currentEBCategories)
                {
                    rowsEBCategories[row][val] = col;
                    colsEBCategories[col][val] = row;
                    if (row == col)
                    {
                        diagonalsEBCategories[0][val] = row;
                    }
                    if (row + col == 4)
                    {
                        diagonalsEBCategories[1][val] = row;
                    }
                }

                return true;
            }
        }

        public BingoGenerator()
        {
            // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
            //		REGION DATA
            // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬

            AllNoRegionTags = new List<string>() { "NotCaelid", "NotLimgrave", "NotLiurnia", "NotAltus", "NotMountaintops", "NotLandOfShadow" };

            // Caelid
            RegionCaelid.RegionName = "Caelid";
            RegionCaelid.RegionID = 0;

            RegionCaelid.LegacyDungeonCount = 1;
            RegionCaelid.CavesCount = 5;
            RegionCaelid.CatacombCount = 1;
            RegionCaelid.DungeonCount = RegionCaelid.CavesCount + RegionCaelid.CatacombCount;
            RegionCaelid.HerosGraveCount = 0;
            RegionCaelid.EvergaolCount = 1;
            RegionCaelid.GreatRuneCount = 1;

            RegionCaelid.OverworldBossCount = 8;
            RegionCaelid.NightBossCount = 4;
            RegionCaelid.MinorErdBossCount = 1;
            RegionCaelid.RuinBossCount = 1;
            RegionCaelid.InvaderCount = 1;

            // Limgrave
            RegionLimgrave.RegionName = "Limgrave";
            RegionLimgrave.RegionID = 1;

            RegionLimgrave.LegacyDungeonCount = 1;
            RegionLimgrave.CavesCount = 7;
            RegionLimgrave.CatacombCount = 4;
            RegionLimgrave.DungeonCount = RegionLimgrave.CavesCount + RegionLimgrave.CatacombCount;
            RegionLimgrave.HerosGraveCount = 1;
            RegionLimgrave.EvergaolCount = 2;
            RegionLimgrave.GreatRuneCount = 1;

            RegionLimgrave.OverworldBossCount = 7;
            RegionLimgrave.NightBossCount = 3;
            RegionLimgrave.MinorErdBossCount = 1;
            RegionLimgrave.RuinBossCount = 1;
            RegionLimgrave.InvaderCount = 4;

            // Liurnia
            RegionLiurnia.RegionName = "Liurnia";
            RegionLiurnia.RegionID = 2;

            RegionLiurnia.LegacyDungeonCount = 1;
            RegionLiurnia.CavesCount = 4;
            RegionLiurnia.CatacombCount = 3;
            RegionLiurnia.DungeonCount = RegionLiurnia.CavesCount + RegionLiurnia.CatacombCount;
            RegionLiurnia.HerosGraveCount = 0;
            RegionLiurnia.EvergaolCount = 3;
            RegionLiurnia.GreatRuneCount = 0;

            RegionLiurnia.OverworldBossCount = 10;
            RegionLiurnia.NightBossCount = 5;
            RegionLiurnia.MinorErdBossCount = 2;
            RegionLiurnia.RuinBossCount = 1;
            RegionLiurnia.InvaderCount = 1;

            // Altus
            RegionAltus.RegionName = "Altus";
            RegionAltus.RegionID = 3;

            RegionAltus.LegacyDungeonCount = 2;
            RegionAltus.CavesCount = 7;
            RegionAltus.CatacombCount = 3;
            RegionAltus.DungeonCount = RegionAltus.CavesCount + RegionAltus.CatacombCount;
            RegionAltus.HerosGraveCount = 3;
            RegionAltus.EvergaolCount = 1;
            RegionAltus.GreatRuneCount = 3;

            RegionAltus.OverworldBossCount = 14;
            RegionAltus.NightBossCount = 3;
            RegionAltus.MinorErdBossCount = 2;
            RegionAltus.RuinBossCount = 2;
            RegionAltus.InvaderCount = 2;

            // Mountaintops
            RegionMountaintops.RegionName = "Mountaintops";
            RegionMountaintops.RegionID = 4;

            RegionMountaintops.LegacyDungeonCount = 2;
            RegionMountaintops.CavesCount = 6;
            RegionMountaintops.CatacombCount = 5;
            RegionMountaintops.DungeonCount = RegionMountaintops.CavesCount + RegionMountaintops.CatacombCount;
            RegionMountaintops.HerosGraveCount = 1;
            RegionMountaintops.EvergaolCount = 2;
            RegionMountaintops.GreatRuneCount = 1;

            RegionMountaintops.OverworldBossCount = 13;
            RegionMountaintops.NightBossCount = 6;
            RegionMountaintops.MinorErdBossCount = 3;
            RegionMountaintops.RuinBossCount = 0;
            RegionMountaintops.InvaderCount = 3;

            // Land of Shadow
            RegionLandOfShadow.RegionName = "LandOfShadow";
            RegionLandOfShadow.RegionID = 5;

            RegionLandOfShadow.LegacyDungeonCount = 2;
            RegionLandOfShadow.CavesCount = 4;
            RegionLandOfShadow.CatacombCount = 3;
            RegionLandOfShadow.DungeonCount = RegionLandOfShadow.CavesCount + RegionLandOfShadow.CatacombCount;
            RegionLandOfShadow.HerosGraveCount = 0;
            RegionLandOfShadow.EvergaolCount = 0;
            RegionLandOfShadow.GreatRuneCount = 0;

            RegionLandOfShadow.OverworldBossCount = 16;
            RegionLandOfShadow.NightBossCount = 0;
            RegionLandOfShadow.MinorErdBossCount = 0;
            RegionLandOfShadow.RuinBossCount = 0;
            RegionLandOfShadow.InvaderCount = 2;

            AllRegionData = new List<RegionData>() { RegionCaelid, RegionLimgrave, RegionLiurnia, RegionAltus, RegionMountaintops, RegionLandOfShadow};

            FinalList = "";
            BoardsGenerated = 0;
        }

        public static void SetUIVariables(CheckBox CBGoalsAllowEvil, CheckBox CBGoalsAllowHitless, CheckBox CBNoCategoryLogic, CheckBox CBNoTierLogic, TrackBar TB_LineMaxTierLogic, TrackBar TB_LineMaxTierNoLogic) 
        {
            CBGoalsAllowEvil.SafeInvoke(()      => BingoGenerator.NoEvil = !CBGoalsAllowEvil.Checked);
            CBGoalsAllowHitless.SafeInvoke(()   => BingoGenerator.NoHitless = !CBGoalsAllowHitless.Checked);
            CBNoCategoryLogic.SafeInvoke(()     => BingoGenerator.NoCategoryLogic = CBNoCategoryLogic.Checked);
            CBNoTierLogic.SafeInvoke(()         => BingoGenerator.NoTierLogic = CBNoTierLogic.Checked);
            TB_LineMaxTierLogic.SafeInvoke(()   => BingoGenerator.TiersPerLine = TB_LineMaxTierLogic.Value);
        }
        public async void GenerateBingoCardAsync(long Seed = -1, int StartingArea = -1, int LoSExitWaygate = -1, bool WriteFiles = true, List<bool> EnabledRegions = null, int EnabledBaseGameRegionAmount = -1)
        {
            GenerateBingoCard(Seed, StartingArea, LoSExitWaygate, false, EnabledRegions, EnabledBaseGameRegionAmount);
        }

        public TallyData GetTallyData()
        {
            if (!TallyMode)
                throw new InvalidOperationException("BingoGenerator's GetTallyData() was called but TallyMode is off."); // Error

            if (BoardsGenerated < 1)
                throw new InvalidOperationException("BingoGenerator's GetTallyData() was called before a board had been generated."); // Error

            TallyData TallyD = new TallyData();
            TallyD.GoalList = MainList;
            TallyD.FinalCategories = FinalCategories;
            TallyD.GeneratedCategories = GeneratedCategories;
            TallyD.SlotsPossibleGoals = SlotsPossibleGoals;
            return TallyD;
        }

        public void GenerateBingoCard(long Seed = -1, int StartingArea = -1, int LoSExitWaygate = -1, bool WriteFiles = true, List<bool> EnabledRegions = null, int EnabledBaseGameRegionAmount = -1)
        {
            if (Globals.DebugMessages)
                Globals.ExecTimer1Start("Bingo Board Generation");

            uint Seed1;

            if (Seed == -1)
                Seed1 = Globals.Seed;
            else
                Seed1 = (uint)Math.Abs(Seed);

            if (StartingArea == -1)
                StartingArea = Globals.FinalStartingRegion;

            if (StartingArea == -1)
                throw new InvalidOperationException("BingoGenerator was started with no valid startingarea. Not even in Globals.FinalStartingRegion."); // Error

            this.StartingArea = StartingArea;

            if (LoSExitWaygate == -1)
                LoSExitWaygate = Globals.LoSExitWaygate;

            if (LoSExitWaygate == -1)
                throw new InvalidOperationException("BingoGenerator was started with no valid LoSExitWaygate. Not even in Globals.LoSExitWaygate."); // Error

            this.LoSExitWaygate = LoSExitWaygate;

            if (EnabledRegions == null)
                this.EnabledRegions = Globals.EnabledRegions;
            else
                this.EnabledRegions = EnabledRegions;

            if (EnabledBaseGameRegionAmount == -1)
                this.EnabledBaseGameRegionAmount = Globals.EnabledBaseGameRegionAmount;
            else
                this.EnabledBaseGameRegionAmount = EnabledBaseGameRegionAmount;

            BingoRand = new RandomNumberGenerator(Seed1);

            SetPossibleDistances();

            ParseGoalCategoryListJSON(ref Categories, ref AnyCategories, ref CategoryNamesByID);
            ParseGoalListJSON(ref FullBaseGoalList, ref Categories, StartingArea, BoardsGenerated);
            ParseChallengeJSON();

            SortGoals();

            // Reading the JSON file
            (Dictionary<int, List<int[,]>> resultsWithChangedFives, Dictionary<int, List<int[,]>> resultsWithFivesUnchanged) = LoadTierGridsFromJson(Globals.DataFolderPathDev + @"bakedBingoTierGrids.json");

            List<int[,]> TierGrids;

            bool UnchangedFives = true;
            double BalanceTemperature = 0.99;

            if (TiersPerLine <= 11 || Globals.EnabledRegionAmount == 1)
            {
                if (TiersPerLine <= 10)
                {
                    BalanceTemperature = 0.80;
                }

                UnchangedFives = false;
            }

            if(UnchangedFives)
                TierGrids = resultsWithFivesUnchanged[TiersPerLine];
            else
                TierGrids = resultsWithChangedFives[TiersPerLine];

            if(TiersPerLine > 9 && TiersPerLine < 15)
                TierGrids = BalanceGrids(TierGrids, BalanceTemperature, UnchangedFives);

            TierIterationCounter = 0;
            bool result = false;

            while (!result)
            {
                GenerateCategories();
                result = PickTiersAndPickGoals(TierGrids);
#if DEBUG
                if (TierIterationCounter % 5000 == 0)
                    Trace.WriteLine(TierIterationCounter.ToString());
                if (!result && LastResetDebugPrints)
                    Trace.WriteLine("Iteration: " + TierIterationCounter + ": " + LastResetReason); // TODO later: check even more LastResetReason
#endif
                if (TierIterationCounter == 30000 && (TiersPerLine > 9 && TiersPerLine < 15))
                {
                    TierGrids = resultsWithChangedFives[TiersPerLine];
                    if (UnchangedFives)
                    {
                        UnchangedFives = false;
                        TierGrids = BalanceGrids(TierGrids, BalanceTemperature, UnchangedFives);
                    }
                    
                }
            }

            if (Globals.DebugMessages)
                Trace.WriteLine("Tier finding iterations: " + TierIterationCounter);

            GenerateGoals();

            if (DebugPrints)
            {
                foreach (Goal Goal in MainList)
                    Trace.WriteLine(Goal.GoalDesc);

                Trace.WriteLine("Starting region: " + Globals.AreaNames[StartingArea]);

                PrintGrid(MainList, 40);
            }

            // Write final goal list, and send to clipboard
            if (WriteFiles)
            {
                WriteResultJSON();
                //Clipboard.SetText(FinalGoalListString());
            }

            BoardsGenerated++;

            ElapsedTime = (float)Globals.ExecTimer1.Elapsed.TotalSeconds;

            if (Globals.DebugMessages)
                Globals.ExecTimer1End();
        }

        public void SetPossibleDistances()
        {
            PossibleDistances.Clear();

            if (!EnabledRegions.Contains(true))
                return;
            
            for (int region = 0; region < Globals.TotalRegionAmount; region++)
            {
                if (EnabledRegions[region] == false)
                    continue;

                int dist = U.RegionDistance(StartingArea, region, LoSExitWaygate);

                if (!PossibleDistances.Contains(dist))
                    PossibleDistances.Add(dist);
            }
        }

        // This function fills "sortedGoals" it is a List<List<List<Goal>>> where: First index = category, Second index = whether guaranteed challenge,
        //                                                                         Third index = tier,     Fourth index = index of goal.

        // This is done so that it doesn't need to search through one list when choosing goals, they can be accessed using the indices.
        public void SortGoals()
        {
            if (SortedGoals.Count > 0)
                SortedGoals = new List<List<List<List<Goal>>>>();

            for (int i = 0; i < Categories.Count(); i++)
            {   // For each category
                SortedGoals.Add(new List<List<List<Goal>>>());
                for (int j = 0; j < 2; j++)
                {   // For whether guaranteed challenge
                    SortedGoals[i].Add(new List<List<Goal>>());
                    for (int u = 0; u < 5; u++)
                    {   // For each possible tier
                        SortedGoals[i][j].Add(new List<Goal>());
                    }
                }
            }

            if (SortedGoalsLimited.Count > 0)
                SortedGoalsLimited = new List<List<List<List<Goal>>>>();

            for (int i = 0; i < Categories.Count(); i++)
            {   // For each category
                SortedGoalsLimited.Add(new List<List<List<Goal>>>());
                for (int j = 0; j < 2; j++)
                {   // For whether guaranteed challenge
                    SortedGoalsLimited[i].Add(new List<List<Goal>>());
                    for (int u = 0; u < 5; u++)
                    {   // For each possible tier
                        SortedGoalsLimited[i][j].Add(new List<Goal>());
                    }
                }
            }

            if (SortedGoalsCommonCategories.Count > 0)
                SortedGoalsCommonCategories = new List<List<List<List<HashSet<GoalCategory>>>>>();

            for (int i = 0; i < Categories.Count(); i++)
            {   // For each category
                SortedGoalsCommonCategories.Add(new List<List<List<HashSet<GoalCategory>>>> ());
                for (int j = 0; j < 5; j++)
                {   // For each possible tier
                    SortedGoalsCommonCategories[i].Add(new List<List<HashSet<GoalCategory>>> ());
                    for (int c = 0; c < 2; c++)
                    {   // For whether purechallenge or not
                        SortedGoalsCommonCategories[i][j].Add(new List<HashSet<GoalCategory>>());
                        for (int u = 0; u < 2; u++)
                        {   // For whether limited or not
                            SortedGoalsCommonCategories[i][j][c].Add(new HashSet<GoalCategory>());
                        }
                    }
                }
            }

            foreach (var GoalInList in FullBaseGoalList)
            {
                // Skip goal if it is region specific to a disabled region.
                if (!CheckRegionLimitSkip(GoalInList))
                    continue;

                HashSet<int> TierRange = new HashSet<int>();
                HashSet<int> BasicTierRange = new HashSet<int>();
                if (GoalInList.Tier < 1)
                    BasicTierRange = GoalInList.PossibleTiers;
                else
                    BasicTierRange = new HashSet<int>() { GoalInList.Tier };

                // If possibly challenge goal we give it a chance but either way we put it in the normal array and save a
                // var saying it is a challenge goal, in addition to saving it there we treat it as a challenge goal and
                // we save it in a separate list layer where everything is challenges
                // If a goal is a challenge goal we need to loop all possible additions.

                // for every required challengeGoal we save it only to the second array/list layer. We later on only use
                // the seperate list layer for when the underlaying category is guaranteed to be a challenge.

                bool IsPossiblyChallengeGoal = (GoalInList.ChallengeGoal == "True");
                bool ChallengeRolledYes = IsPossiblyChallengeGoal && (BingoRand.Next() % 100 < 25);
                bool DontSaveAsNormalGoal = GoalInList.ChallengeGoal == "Required";                      // OR failed ChallengeRolledYes when on second iteration
                bool SaveAsChallengeGoal = GoalInList.ChallengeGoal == "Required" || ChallengeRolledYes;

                bool hasIteratedOnce = false;

            StartOfGoal:

                Goal Goal = new Goal(GoalInList);

                HashSet<int> NewPossibleTiers = new HashSet<int>();

                if (SaveAsChallengeGoal) // Get the possible tiers and add them to List<int> TierRange
                {
                    bool FoundValidChallenge = false;
                    foreach (var Challenge in FullChallengeModifierList) // For every possible challenge
                    {
                        bool BossPoolIsExcluded = false;
                        foreach (var ExcludedBossPool in Challenge.ExcludedBossPools)
                            if (Goal.GoalCategories.Contains(Categories[ExcludedBossPool]))
                            {
                                BossPoolIsExcluded = true;
                                break;
                            }

                        if (Challenge.ChallengeTier <= Goal.ChallengeRange && !Challenge.ForbiddenGoals.Contains(Goal.GoalDesc) && !BossPoolIsExcluded) // If the challenge fits this goal
                        {
                            foreach (var Tier in BasicTierRange)
                            {
                                for (int Solo = 0; Solo < (Challenge.SoloEligible == "Yes" && !Goal.GoalCategories.Contains(Categories["Evergaols"]) ? 2 : 1); Solo++)
                                {
                                    int FinalTier = Tier + Challenge.BaseTierAdd + Solo;

                                    if (Globals.ModifiersEnabled)
                                    {
                                        foreach (var ModifierTierChange in Challenge.ModifierTierChanges)
                                        {
                                            for (int ModifierIndex = 0; ModifierIndex < Globals.CurrentModifierCount; ModifierIndex++)
                                            {
                                                string ModifierName = Globals.CurrentModifierList[ModifierIndex].Name;
                                                if (ModifierName == ModifierTierChange.Key)
                                                {
                                                    FinalTier += ModifierTierChange.Value;
                                                }
                                            }
                                        }
                                    }

                                    if (NoTierLogic && FinalTier > 5 && FinalTier < 8)
                                        FinalTier = 5;

                                    if (FinalTier >= 1 && FinalTier <= 5 && !TierRange.Contains(FinalTier))
                                    {
                                        FoundValidChallenge = true;
                                        TierRange.Add(FinalTier);
                                    }

                                }
                            }
                        }
                    }
                    if (FoundValidChallenge)
                        Goal.AssignedChallengeGoal = true;
                }
                else
                {
                    if (Goal.Tier < 1)
                        TierRange = Goal.PossibleTiers;
                    else
                        TierRange = new HashSet<int>() { Goal.Tier };
                }
                
                // Choose a main category for this Goal
                int ChoosenMainCategory = 0;

                if(Goal.GoalCategories.Count > 1)
                {
                    List<GoalCategory> Categories = new List<GoalCategory>();
                    foreach (var Category in Goal.GoalCategories)
                    {
                        if(Category.CanReplaceAny == true && Category.Chance > 0)
                            Categories.Add(Category);
                    }
                    if (Categories.Count > 1)
                    {
                        // Start out with equal change for each, then empty has 100%, then the least full has all the chance, if tie they split the chance

                        int LeastCount = 0;
                        List<bool> Least = new List<bool>();
                        int Min = 9999999;
                        foreach (var Category in Categories)
                        {
                            int CategoryGoalCount = GetSortedGoalsSubsetCount(ref SortedGoals, Category.ID);

                            if (CategoryGoalCount < Min)
                                Min = CategoryGoalCount;
                        }
                        foreach (var Category in Categories)
                        {
                            int CategoryGoalCount = GetSortedGoalsSubsetCount(ref SortedGoals, Category.ID);

                            if (CategoryGoalCount == Min)
                            {
                                LeastCount++;
                                Least.Add(true);
                            }
                            else
                                Least.Add(false);
                        }

                        if(LeastCount > 1)
                        {
                            List<double> Chances = new List<double>();
                            for (int i = 0 ; i < LeastCount ; i++)
                            {
                                Chances.Add( 100.0 / LeastCount );
                            }
                            int idx = U.ChooseFromChances(ref BingoRand, Chances);
                            ChoosenMainCategory = Categories[idx].ID;
                        }
                        else
                        {
                            int idx = 0;
                            foreach (bool IsLeast in Least)
                            {
                                if (IsLeast)
                                    ChoosenMainCategory = Categories[idx].ID;

                                idx++;
                            }
                        }

                        
                    }
                    else if (Categories.Count == 1)
                        ChoosenMainCategory = Categories[0].ID;
                    else
                        ChoosenMainCategory = Goal.GoalCategories[0].ID;
                }
                else
                {
                    ChoosenMainCategory = Goal.GoalCategories[0].ID;
                }


                foreach (var GoalCategory in Goal.GoalCategories)
                {
                    HashSet<int> AddedTiers = new HashSet<int>();

                    bool VariableRegion = false;

                    if (Goal.GoalDesc.Contains("|Region|"))
                        VariableRegion = true;

                    int ModifierTierChangeAmount = 0;

                    // ModifierTierChanges
                    if (Globals.ModifiersEnabled)
                    {
                        foreach (var ModifierTierChange in Goal.ModifierTierChanges)
                        {
                            for (int ModifierIndex = 0; ModifierIndex < Globals.CurrentModifierCount; ModifierIndex++)
                            {
                                string ModifierName = Globals.CurrentModifierList[ModifierIndex].Name;
                                if (ModifierName == ModifierTierChange.Key)
                                {
                                    if (ModifierTierChange.Value <= -100 || ModifierTierChange.Value >= 100)
                                        goto GoalDone; // The goal is removed thanks to an incompatible modifier.

                                    ModifierTierChangeAmount += ModifierTierChange.Value;
                                }
                            }
                        }
                    }
                    Goal.ModifierChangedAmount = ModifierTierChangeAmount;

                    foreach (var Tier in TierRange)
                    {
                        for (int D = 0; (!VariableRegion && D < 1) || (VariableRegion && D < PossibleDistances.Count); D++)
                        {
                            int FinalTier = Tier;

                            FinalTier += ModifierTierChangeAmount;

                            // Defined Region
                            if (Goal.Region != -1)
                            {
                                int SpecificDistance = U.RegionDistance(StartingArea, Goal.Region, LoSExitWaygate);
                                //if (SpecificDistance > MaxDistance) 
                                //    goto GoalDone;
                                FinalTier += SpecificDistance;
                            }

                            // Possible distance from variable region
                            if (VariableRegion)
                                FinalTier += PossibleDistances[D];

                            // Special exception to include further legacy dungeons sometimes
                            if (FinalTier == 6 && (GoalCategory.Name == "LegacyDungeons" || GoalCategory.Name == "LegacyDungeonSpecific")
                                && BingoRand.Next() % 100 < 50 + Goal.BonusProbability)
                                FinalTier = 5;

                            if(NoTierLogic && FinalTier > 5 && FinalTier < 8)
                                FinalTier = 5;

                            if (FinalTier >= 1 && FinalTier <= 5 && !AddedTiers.Contains(FinalTier))
                            {
                                if (!DontSaveAsNormalGoal)
                                {
                                    SortedGoals[GoalCategory.ID][0][FinalTier - 1].Add(Goal);

                                    if(GoalCategory.ID == ChoosenMainCategory)
                                        SortedGoalsLimited[ChoosenMainCategory][0][FinalTier - 1].Add(Goal);
                                }

                                if (SaveAsChallengeGoal)
                                {
                                    SortedGoals[GoalCategory.ID][1][FinalTier - 1].Add(Goal);

                                    if (GoalCategory.ID == ChoosenMainCategory)
                                        SortedGoalsLimited[ChoosenMainCategory][1][FinalTier - 1].Add(Goal);

                                    // If TierChallengeBase hasn't been assigned or current FinalTier is lower that the assigned one.
                                    if (Goal.TierChallengeBase == -1 || FinalTier - 1 < Goal.TierChallengeBase)
                                        Goal.TierChallengeBase = FinalTier - 1;
                                }

                                AddedTiers.Add(FinalTier);
                                NewPossibleTiers.Add(FinalTier);
                            }
                        }
                    }
                }

                Goal.PossibleTiers = NewPossibleTiers;

                if (IsPossiblyChallengeGoal && !ChallengeRolledYes && !hasIteratedOnce)
                {
                    hasIteratedOnce = true; // Start over but not as a normal goal but just as a challenge goal
                    DontSaveAsNormalGoal = true;
                    SaveAsChallengeGoal = true;
                    TierRange = new HashSet<int>();
                    goto StartOfGoal;
                }

            GoalDone:;
            }

            // Post goal-sorting category checks

            foreach (var Category in Categories)
            {
                bool HasNonChallengeOnlyGoals = false;
                bool HasChallengeOnlyGoals = false;
                CategoryHasGoals(SortedGoals[Category.Value.ID], ref HasNonChallengeOnlyGoals, ref HasChallengeOnlyGoals);

                if (!HasNonChallengeOnlyGoals && HasChallengeOnlyGoals)
                {
                    // Change the category to be challenge only since it only has challenge goals

                    GoalCategory tempGC = Categories[Category.Key];
                    tempGC.IsChallengeOnly = true;
                    Categories[Category.Key] = tempGC; // If there are only challenge goals the category is challenge only
                }
                else if (Category.Value.IsChallengeOnly && HasNonChallengeOnlyGoals && !HasChallengeOnlyGoals)
                {
                    // Change the category to be non challenge, since it has only non challenge goals but is marked incorrectly
                    
                    GoalCategory tempGC = Categories[Category.Key];
                    tempGC.IsChallengeOnly = false;
                    Categories[Category.Key] = tempGC; // If there are only challenge goals the category is challenge only
                }
                else if (!HasNonChallengeOnlyGoals && !HasChallengeOnlyGoals && AnyCategories.Contains(Category.Key))
                    AnyCategories.Remove(Category.Key);

                List<string> BuddyCategories = new List<string>();
                bool AtFirst = true;
                int GoalCount = 0; // Any category that has less than five goals gets set to have that number of max categories per board, this might help bad tier finding iterations
                foreach (var Goal in FullBaseGoalList)
                {
                    if (Goal.GoalCategories.Contains(Category.Value))
                    {
                        GoalCount++;

                        if (BoardsGenerated > 0)
                            continue; // The following only needs to be done once.

                        // --- Find BuddyCategories ---
                        if (AtFirst)
                        {
                            AtFirst = false;

                            foreach (var GoalCategory in Goal.GoalCategories)
                            {
                                if (GoalCategory.Name != Category.Value.Name && !Category.Value.ExclusivityBuddies.Contains(GoalCategory.Name))
                                    BuddyCategories.Add(GoalCategory.Name);
                            }
                        }
                        else
                        {
                            for (int BuddyCategoryNameIdx = 0 ; BuddyCategoryNameIdx < BuddyCategories.Count() ; BuddyCategoryNameIdx++)
                            {
                                bool found = false;
                                foreach (var GoalCategory in Goal.GoalCategories)
                                {
                                    if (GoalCategory.Name == BuddyCategories[BuddyCategoryNameIdx])
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    BuddyCategories.RemoveAt(BuddyCategoryNameIdx);
                                    BuddyCategoryNameIdx--;
                                }
                            }
                        }
                    }
                }

                if (BuddyCategories.Any())
                {
                    GoalCategory tempGC = Categories[Category.Key];
                    tempGC.CategoryBuddies = new HashSet<string>(BuddyCategories);
                    Categories[Category.Key] = tempGC;
                }

                if (Category.Value.MaxPerBoard > GoalCount && GoalCount < 5)
                {
                    GoalCategory tempGC = Categories[Category.Key];
                    tempGC.MaxPerBoard = GoalCount;
                    Categories[Category.Key] = tempGC;
                }

                if(GoalCount == 1 && !Category.Value.SelfExclusive) // If the category only has one goal it gets set to be Self Exclusive to prevent hopeless generations.
                {
                    GoalCategory tempGC = Categories[Category.Key];
                    tempGC.SelfExclusive = true;
                    Categories[Category.Key] = tempGC;
                }
            }

            // Setup AnyCategories' chances dict. Do this as soon as the AnyCategories list is final.
            {
                AnyCategoriesChances.Clear();

                List<int> UnNormalizedChances = new List<int>();

                foreach (string Category in AnyCategories)
                {
                    UnNormalizedChances.Add(Categories[Category].Chance);
                }

                List<double> NormalizedChances = U.NormalizeValuesWithTemperature0to1(UnNormalizedChances);

                for (int c = 0; c < UnNormalizedChances.Count; c++)
                {
                    AnyCategoriesChances.Add(AnyCategories[c], NormalizedChances[c]);
                }
            }

            // Second category loop
            // Setup SortedGoalsCommonCategories, which is a list of all the categories that are shared by all the goals
            // in that category-tier combo. It's deepest list as two entries, one for full SortedGoals and one for SortedGoalsLimited.
            foreach (var Category in Categories)
            {
                for(int c = 0 ; c < 2 ; c++)
                {
                    for (int t = 0; t < 5; t++)
                    {
                        List<Goal> Combo = SortedGoals[Category.Value.ID][c][t];
                        if (Combo.Count > 0)
                        {
                            List<GoalCategory> sharedCategories = null;
                            bool AtFirst = true;

                            foreach (var Goal in Combo)
                            {
                                if (AtFirst)
                                    sharedCategories = new List<GoalCategory>(Goal.GoalCategories);
                                else
                                {
                                    for (int sc = 0; sc < sharedCategories.Count; sc++)
                                    {
                                        if (!Goal.GoalCategories.Contains(sharedCategories[sc]))
                                        {
                                            sharedCategories.RemoveAt(sc);
                                            sc--;
                                        }
                                    }
                                }
                                AtFirst = false;
                            }

                            foreach(GoalCategory sharedCategory in sharedCategories)
                                SortedGoalsCommonCategories[Category.Value.ID][t][c][0].Add(sharedCategory);
                        }

                        if (SortedGoalsLimited[Category.Value.ID][c][t].Count > 0)
                        {
                            List<GoalCategory> sharedCategories = null;
                            bool AtFirst = true;

                            foreach (var Goal in SortedGoalsLimited[Category.Value.ID][c][t])
                            {
                                if (AtFirst)
                                    sharedCategories = new List<GoalCategory>(Goal.GoalCategories);
                                else
                                {
                                    for (int sc = 0; sc < sharedCategories.Count; sc++)
                                    {
                                        if (!Goal.GoalCategories.Contains(sharedCategories[sc]))
                                        {
                                            sharedCategories.RemoveAt(sc);
                                            sc--;
                                        }
                                    }
                                }
                                AtFirst = false;
                            }

                            foreach (GoalCategory sharedCategory in sharedCategories)
                                SortedGoalsCommonCategories[Category.Value.ID][t][c][1].Add(sharedCategory);
                        }
                    }
                }
            }

            if (BoardsGenerated < 1)// Only need to do this once
            {
                // -Setup received1WCategoryBuddies-
                // Third category loop
                foreach (var Category in Categories)
                {
                    // For each category buddy in current category
                    foreach (string CBuddy in Category.Value.CategoryBuddies)
                    {
                        // If the buddy doesn't have the current category as a buddy back
                        if (!Categories[CBuddy].CategoryBuddies.Contains(Category.Key))
                        {
                            GoalCategory tempGC = Categories[CBuddy];
                            tempGC.Received1WCategoryBuddies.Add(Category.Key);
                            Categories[CBuddy] = tempGC;
                        }
                    }
                }


                // --- Fix ExclusivityBuddies based on CategoryBuddies ---

                // Fourth Category loop
                foreach (var Category in Categories)
                {
                    GoalCategory tempGC = Category.Value;
                    bool AddedAny = false;
                    
                    // For each category buddy in the current category
                    foreach (string CBuddy in Category.Value.CategoryBuddies)
                    {
                        // For each exclusivity buddy in the category buddy of the current category
                        foreach (string EB in Categories[CBuddy].ExclusivityBuddies)
                        {
                            // Add it as EB
                            tempGC.ExclusivityBuddies.Add(EB);
                            AddedAny = true;

                            QuickEBAdd(Category.Key, EB); // Make it two-way right away
                        }
                        
                        if (Categories[CBuddy].SelfExclusive)
                        {
                            tempGC.SelfExclusive = true; // If a category's cbuddy is SE, it should get set to also be SE.
                            tempGC.ExclusivityBuddies.Add(CBuddy); // And the two buddies need to be EB as well.
                            AddedAny = true;

                            QuickEBAdd(Category.Key, CBuddy); // Make it two-way right away
                        }
                    }

                    // For each exclusivity buddy in the current category
                    int EBCount = tempGC.ExclusivityBuddies.Count;
                    for (int EB = 0 ; EB < tempGC.ExclusivityBuddies.Count; EB++)
                    {
                        // For each one-way received buddy connection in the exclusivity buddy
                        foreach (string CB1W in Categories[tempGC.ExclusivityBuddies.ElementAt(EB)].Received1WCategoryBuddies)
                        {
                            if(CB1W != Category.Key && !tempGC.ExclusivityBuddies.Contains(CB1W))
                            {
                                tempGC.ExclusivityBuddies.Add(CB1W);
                                AddedAny = true;

                                QuickEBAdd(Category.Key, CB1W); // Make it two-way right away
                            }
                        }
                    }

                    if (AddedAny)
                        Categories[Category.Key] = tempGC;

                    Category.Value.UpdateEBIDs(ref Categories);
                }
            }
        }

        private void QuickEBAdd(string category, string EB)
        {
            if (!Categories[EB].ExclusivityBuddies.Contains(category))
            {
                GoalCategory tempGC = Categories[EB];
                tempGC.ExclusivityBuddies.Add(category);
                Categories[EB] = tempGC;
            }
        }

        private bool CheckRegionLimitSkip(Goal goal)
        {
            // Skip goal if it is region specific to a disabled region.
            if (goal.Region != -1)
            {
                if (EnabledRegions[goal.Region] == false)
                    return false;
            }

            // Skip goal if enabled regions is below the minimum
            if (goal.MinimumRegionsIncludeLoS)
            {
                if (goal.MinimumRegions > Globals.EnabledRegionAmount)
                    return false;
            }
            else
            {
                if (goal.MinimumRegions > EnabledBaseGameRegionAmount)
                    return false;
            }

            // If a goal has LegacyXX as a category, we need to check where the entrance is and see if the entrance region is allowed.
            if (goal.GoalCategories.Contains(Categories["LegacyRC"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Redmane Castle").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacySC"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Stormveil Castle").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacyRL"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Academy of Raya Lucaria").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacyVM"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Volcano Manor").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacyLD"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Leyndell").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacyMH"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Miquella's Haligtree").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.
            if (goal.GoalCategories.Contains(Categories["LegacyFA"]))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Crumbling Farum Azula").RealRegion)])
                    return false; // We use Globals.PostGeneratedDungeonList and check if the "RealRegion" of the dungeon (interior) is enabled.

            // The same with patches goal, based on the name of the goal
            if (goal.GoalDesc.Contains("Find Patches' Cave") || goal.GoalDesc.Contains("Kill Patches"))
                if (!EnabledRegions[(int)(Globals.PostGeneratedDungeonList.Find(x => x.Name == "Murkwater Cave").RealRegion)])
                    return false;

            if (goal.GoalCategories.Contains(Categories["Deeproot"]) && !EnabledRegions[(int)Region.Altus])
                return false; // Skip deeproot (includes upper ainsel) if Altus is disabled

            if (goal.GoalCategories.Contains(Categories["LandOfShadow"]) && !EnabledRegions[(int)Region.LandOfShadow])
                return false; // Obviously skip LandOfShadow goes when its disabled

            if (goal.GoalCategories.Contains(Categories["ForbiddenLands"]) && !(EnabledRegions[(int)Region.Altus] && EnabledRegions[(int)Region.Mountaintops]))
                return false; // Skip forbiddenlans goals unless if both Altus and MT is enabled

            if (Globals.EnabledRegionAmount < 2 )
            {
                if (goal.GoalCategories.Contains(Categories["BossesUnderworld"]))
                    return false;

                if (goal.GoalDesc == "Any ruin boss")
                {
                    if (AllRegionData[StartingArea].RuinBossCount < 1)
                        return false;
                    else if (AllRegionData[StartingArea].RuinBossCount == 1)
                        goal.Tier++;
                }

                if (goal.GoalDesc == "Any 2 Legacy Dungeons")
                {
                    if (!EnabledRegions[(int)Region.Altus] && !EnabledRegions[(int)Region.Mountaintops])
                        return false;
                    else
                    {
                        goal.ForbiddenGoals.Add("[Volcano Manor]");
                        goal.ForbiddenGoals.Add("[Crumbling Farum Azula]");
                    }
                }

                // If the only region is tagged as impossible, skip.
                if (goal.GoalCategories.Contains(Categories[AllNoRegionTags[StartingArea]])) 
                    return false;
            }

            // Check tags to make sure there is there is a enabled region that isn't tagged as NotX 
            bool FoundValidRegion = false;
            for (int Region = 0; Region < Globals.TotalRegionAmount; Region++)
            {
                if (EnabledRegions[Region] && !goal.GoalCategories.Contains(Categories[AllNoRegionTags[Region]]))
                {
                    FoundValidRegion = true;
                    break;
                }
            }

            if (!FoundValidRegion)
            {
                foreach (var NoRegionTag in AllNoRegionTags)
                {
                    if(goal.GoalCategories.Contains(Categories[NoRegionTag]))
                        return false;
                }
            }

            return true;
        }

        public void GenerateCategories()
        {
        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        //		Category Generation
        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬

        StartOfCategoryGen:

            // Read BoardDesc
            List<GoalCategory> GeneratedCategories = new List<GoalCategory>(new GoalCategory[25]);
            ParseBoardDescJSON("BoardDesc", ref GeneratedCategories);

            // Makes a temporary array to use the CheckGrid function.
            // The CheckGrid function checks that the catagories are exclusive to their row.
            GoalCategory[,] GoalCategoriesGrid = new GoalCategory[5, 5];

            int shuffleIterations = 0;

        StartOfShuffle:

            // Place the BoardDesc categories

            Shuffle(GeneratedCategories);

            if(!NoCategoryLogic)
            {
                List<GoalCategory> TempGeneratedCategories = new List<GoalCategory>(GeneratedCategories);
                TempGeneratedCategories.RemoveAll(x => x.Name == "Any");

                CategoriesGridChecker GridChecker0 = new CategoriesGridChecker();

                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        int Index = (row * 5) + col;
                        //GoalCategoriesGrid[j, k] = GeneratedCategories[Index];

                        if (GeneratedCategories[Index].Name == "Any")
                        {
                            GoalCategoriesGrid[row, col] = GeneratedCategories[Index];
                            continue;
                        }

                        List<GoalCategory> CatsToTry = GridChecker0.GetAvailableCategoriesForCell(ref TempGeneratedCategories, Index);

                        if(CatsToTry.Count > 0)
                        {
                            GoalCategory Category = CatsToTry[BingoRand.Next(CatsToTry.Count)];

                            GoalCategoriesGrid[row, col] = Category;
                            GridChecker0.AddSlot(row, col, Category.ID, Category.ExclusivityBuddiesIDs);

                            TempGeneratedCategories.Remove(Category);
                        }
                        else // If there where no valid categories for this slot
                        {
                            shuffleIterations++;

                            if (shuffleIterations < 120000) // Failsafe
                                goto StartOfShuffle;
                            else
                            {
                                if (DebugPrints || LastResetDebugPrints) Trace.WriteLine("shuffleIterations: " + shuffleIterations.ToString());
                                goto StartOfCategoryGen;
                            }
                        }
                    }
                }

                if (!CheckGridCategory(GoalCategoriesGrid))
                {
                    if (shuffleIterations < 120000) // Failsafe
                        goto StartOfShuffle;
                    else
                    {
                        if (DebugPrints || LastResetDebugPrints) Trace.WriteLine("shuffleIterations: " + shuffleIterations.ToString());
                        goto StartOfCategoryGen;
                    }
                }
            }
			
            Dictionary<string, int> GeneratedCategoriesCount = new Dictionary<string, int>();

            foreach (var Category in Categories)
                GeneratedCategoriesCount.Add(Category.Key, 0);

            foreach (var GenCategory in GeneratedCategories)
                GeneratedCategoriesCount[GenCategory.Name]++;

            CategoriesGridChecker GridChecker = new CategoriesGridChecker();

            if (!NoCategoryLogic)
            {
                GoalCategoriesGrid = new GoalCategory[5, 5];
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        int Index = (row * 5) + col;
                        GoalCategoriesGrid[row, col] = GeneratedCategories[Index];
                    }
                }

                for (int u = 0; u < 25; u++)
                {
                    if (GeneratedCategories[u].Name != "Any")
                    {
                        GridChecker.CheckGridCategory(GoalCategoriesGrid, u); // Run this to add them to the hashmaps
                    }
                }
            }

            // Randomize categories for "Any" Categories
            for (int Index = 0; Index < 25; Index++)
            {
                if (GeneratedCategories[Index].Name == "Any")
                {
                    int randomIndex = -1;
                    int NrOfIterations = 0;

                StartOfCategoryRandomization:
                    NrOfIterations++;
                    
                    int ChosenPossibility;
                    do
                    { ChosenPossibility = U.ChooseFromChances0to1(ref BingoRand, AnyCategoriesChances.Values.ToList()); }
                    while (ForbiddenAnyCategories.Contains(ChosenPossibility));

                    GoalCategory RandomCategory = Categories[AnyCategories[ChosenPossibility]];

                    // Check if the maximum number of the chosen category already have been generated
                    if (GeneratedCategoriesCount[RandomCategory.Name] >= RandomCategory.MaxPerBoard && !NoCategoryLogic)
                        goto StartOfCategoryRandomization;

                    GeneratedCategories[Index] = RandomCategory;

                    int row = Index / 5;
                    int col = Index % 5;

                    GoalCategoriesGrid[row, col] = GeneratedCategories[Index];

                    if (!NoCategoryLogic && !GridChecker.CheckGridCategory(GoalCategoriesGrid, Index))
                        goto StartOfCategoryRandomization;

                    GeneratedCategoriesCount[GeneratedCategories[Index].Name]++;
                    GeneratedCategoriesCount["Any"]--;
                }
            }
			
            if (MainList.Count != 0)
                MainList.Clear();

            for (int i = 0; i < 25; i++)
            {
                MainList.Add(new Goal());
                MainList[i].GoalCategories.Add(GeneratedCategories[i]);
            }
        }

        public bool PickTiersAndPickGoals(List<int[,]> TierGrids)
        {
            // 1. Calculate tier range per cell
            for (int i = 0; i < 25; i++)
            {
                GoalCategory CurrentFirstGoalCategory = MainList[i].GoalCategories[0];

                int IsCategoryChallengeOnly = CurrentFirstGoalCategory.IsChallengeOnly ? 1 : 0;

                List<int> tiersToCheck = CurrentFirstGoalCategory.RequiredTiers;
                if (tiersToCheck.Count < 1)
                    tiersToCheck = new List<int> { 1, 2, 3, 4, 5 };

                foreach (int tier in tiersToCheck) // Loop of 5 tiers or only the valid ones if limited
                    if (GetSortedGoalList(CurrentFirstGoalCategory.ID, IsCategoryChallengeOnly, tier - 1).Count() > 0)// Does the category contain goals of the current tier.
                        MainList[i].PossibleTiers.Add(tier);

                if (MainList[i].PossibleTiers.Count() < 1)
                {
                    TierIterationCounter++;
#if DEBUG
                    LastResetReason = "A cell completely lacked any possible tiers!";
#endif
                    return false;
                }
            }

            CheckGridDictionaries CGD = GetMainListCGD();
            for (int i = 0; i < 25; i++)
            {
                CheckGridPossibleTiers(i, CGD);
                if (MainList[i].PossibleTiers.Count() < 1)
                {
                    TierIterationCounter++;
#if DEBUG
                    LastResetReason = "A cell completely lacked any possible tiers after CheckGridPossibleTiers!";
#endif
                    return false;
                }
            }

            int[,] finalTierGrid = null;

            List<GridData> ValidGrids = new List<GridData>();

            int TierGridIndex = 0;
            int TransformationIndex = 0;

            if (!NoTierLogic && UseFasterTierGridChecking)
                Shuffle(TierGrids);

            FasterTierGridCheckingIterationSpot:

            if (!NoTierLogic)
            {
                if (UseFasterTierGridChecking)
                {
                    bool TierFit = false;

                    while (!TierFit && TierGridIndex < TierGrids.Count)
                    {
                        int[,] TierGrid = TierGrids[TierGridIndex];

                        TierFit = CheckTierFit(TierGrid, MainList);

                        if (TierFit)
                        {
                            finalTierGrid = TierGrid;
                            goto TierGridLoopDone;
                        }

                        TierGridIndex++;
                    }
                    TierGridLoopDone:

                    if (!TierFit)
                    {
                        TierIterationCounter++;
#if DEBUG
                        LastResetReason = "Couldn't find a fitting tier grid";
#endif
                        return false;
                    }
                }
                else // -Slower tier grid checking-
                {    // Finds every valid grid and sorts them instead of picking the first valid one.
                    while (TierGridIndex < TierGrids.Count)
                    {
                        int[,] TierGrid = TierGrids[TierGridIndex];

                        if (CheckTierFit(TierGrid, MainList))
                        {
                            GridData ValidGridData = new GridData();
                            ValidGridData.Grid = TierGrid.Clone() as int[,];
                            ValidGrids.Add(ValidGridData);
                        }

                        TransformationIndex = 0;
                        TierGridIndex++;
                    }
#if DEBUG
                    LastResetReason = "ValidGrids.Count: " + ValidGrids.Count + ". ";
#endif
                    if (ValidGrids.Count < 1)
                    {
                        TierIterationCounter++;

                        return false;
                    }
                    else if (ValidGrids.Count == 1)
                    {
                        finalTierGrid = ValidGrids[0].Grid;
                    }
                    else
                    {
                        for (int i = 0; i < ValidGrids.Count; i++)
                        {
                            int score = 0;
                            for (int index = 0; index < 25; index++)
                            {
                                int tier = ValidGrids[i].Grid[index / 5, index % 5]; // Get the choosen tier for this grid space
                                int CategoryID = (int)MainList[index].GoalCategories[0];
                                int IsCategoryChallengeOnly = MainList[index].GoalCategories[0].IsChallengeOnly ? 1 : 0;

                                score += GetSortedGoalList(CategoryID, IsCategoryChallengeOnly, tier - 1).Count;
                            }
                            GridData GD = ValidGrids[i];
                            GD.Score = score;
                            ValidGrids[i] = GD;
                        }

                        ValidGrids = ValidGrids.OrderByDescending(o => o.Score).ToList();
                    }
                }
            }
            int ValidGridIndex = -1;

        SlowerTierGridCheckingIterationSpot:
            if(!UseFasterTierGridChecking && !NoTierLogic)
            {
                ValidGridIndex++;

                if (ValidGridIndex < ValidGrids.Count)
                    finalTierGrid = ValidGrids[ValidGridIndex].Grid;
                else
                {
                    TierIterationCounter++;
                    return false;
                }
                    
            }

            List<Goal> OldMainList = new List<Goal>();
            OldMainList.InsertRange(0, MainList);

            Goal[,] GoalCheckingGrid = new Goal[5, 5];
            Dictionary<int, List<string>> BannedGoalsForSlots = new Dictionary<int, List<string>>();

            // --- Generate and shuffle/sort all possible goals lists ---
            List<List<Goal>> AllPossibleGoals = new List<List<Goal>>();

            for (int index = 0; index < 25; index++) 
            {
                int tier = 1;
                int CategoryID = (int)OldMainList[index].GoalCategories[0];
                int IsCategoryChallengeOnly = OldMainList[index].GoalCategories[0].IsChallengeOnly ? 1 : 0;

                if (!NoTierLogic)
                    tier = finalTierGrid[index / 5, index % 5]; // Get the choosen tier for this grid space

                // "sortedGoals" contains a list for each category and that list has a list for each possible
                // tier. Here we copy the category-and-tier-specific list so we can go through it.
                List<Goal> PossibleGoals;

                if (!NoTierLogic)
                    PossibleGoals = new List<Goal>(GetSortedGoalList(CategoryID, IsCategoryChallengeOnly, tier - 1));
                else
                {
                    PossibleGoals = new List<Goal>(GetSortedGoalList(CategoryID, IsCategoryChallengeOnly, 0));
                    for (int i = 1; i < 5; i++)
                        PossibleGoals.AddRange(GetSortedGoalList(CategoryID, IsCategoryChallengeOnly, i));

                    List<string> GoalNames = new List<string>();
                    for (int i = 0; i < PossibleGoals.Count(); i++)
                    {
                        if (GoalNames.Contains(PossibleGoals[i].GoalDesc))
                        {
                            PossibleGoals.RemoveAt(i);
                            i--;
                        }
                        else
                            GoalNames.Add(PossibleGoals[i].GoalDesc);
                    }
                }

                // Randomize their order
                Shuffle(PossibleGoals);

                // BonusProbability
                // Goals can have a bonus probability, it is a chance for the goal to be moved to the top of the list of possible goals.
                // If the probability is negative it gets moved to the bottom of the list instead, but the chance of it happening is the absolute value.
                if (PossibleGoals.Count > 1)
                {
                    for (int i = 0; i < PossibleGoals.Count; i++)
                    {
                        Goal Goal = PossibleGoals[i];

                        int BonusProbability = Goal.BonusProbability;

                        if (NoTierLogic)
                        {
                            BonusProbability = BonusProbability / 9;
                        }

                        if (BonusProbability != 0)
                        {
                            bool Positive = BonusProbability > 0;
                            int Prob = Math.Abs(BonusProbability);

                            if ((BingoRand.Next() % 100) + 1 <= Prob) // Whether the random chance with the probability succeeds
                            {
                                PossibleGoals.Remove(Goal);
                                PossibleGoals.Insert(Positive ? 0 : PossibleGoals.Count, Goal);
                            }
                        }
                    }
                }

                AllPossibleGoals.Add(PossibleGoals);
            }

            StartOfGoalSelection:
#if DEBUG
            LastResetReason += " - ";
#endif
            GoalsGridChecker GridChecker = new GoalsGridChecker();

            // --- Goal choosing loop ---
            for (int index = 0; index < 25; index++) // Loop through MainList which has the choosen categories.
            {
#if DEBUG
                LastResetReason += ",";
#endif
                int tier;
                bool valid = true;

                int problematicGoalIndex = -1;

                if (!NoTierLogic)
                    tier = finalTierGrid[index / 5, index % 5]; // Get the choosen tier for this grid space
                else
                {
                    tier = (BingoRand.Next() % 5) + 1; // If tier logic is turned off we randomize a tier for this cell

                    int CategoryID = (int)OldMainList[index].GoalCategories[0];
                    int IsCategoryChallengeOnly = OldMainList[index].GoalCategories[0].IsChallengeOnly ? 1 : 0;

                    while (GetSortedGoalList(CategoryID, IsCategoryChallengeOnly, tier - 1).Count < 1) // If there are no goals for the tier we keep randomizing
                        tier = (BingoRand.Next() % 5) + 1;                                             // until there are
                }

                // Loop through every category-and-tier-specific goal
                foreach (var Goal in AllPossibleGoals[index])
                {
                    valid = true;
#if DEBUG
                    LastResetReason += ".";
#endif
                    // Check if the current goal was banned in a previous iteration.
                    if (BannedGoalsForSlots.ContainsKey(index))
                    {
                        if (BannedGoalsForSlots[index].Count >= AllPossibleGoals[index].Count)
                        { // If the banned goals outnumber the possible goals just exit the goals loop early

                            if(NoTierLogic)
                            {
                                TierIterationCounter++;
#if DEBUG
                                LastResetReason += "The banned goals outnumber the possible goals while in NoTierLogic";
#endif
                                return false;
                            }
#if DEBUG
                            LastResetReason += "BanOutnumberPossible";
#endif
                            valid = false;
                            break;
                        }
                        foreach (var Name in BannedGoalsForSlots[index])
                        {
                            if (Goal.GoalDesc == Name)
                            {
                                // If the current goal was banned in a previous iteration.
                                valid = false;
#if DEBUG
                                LastResetReason += "PrevBan";
#endif
                                break; // Break the ban checking loop
                            }
                        }
                    }

                    // Check to see if the goal's name is already on the board so it doesn't appear twice.
                    for (int i = 0; i < index && valid; i++) // Loop through the previously placed goals
                    {
                        if (Goal.GoalDesc == MainList[i].GoalDesc)
                        {
                            valid = false;
                            problematicGoalIndex = i;
#if DEBUG
                            LastResetReason += "OnBoard";
#endif
                            goto NameCheckingLoopDone; // Break the name checking loop
                        }
                        else
                        {
                            // Check if the previously placed goal is forbidden by this goal.
                            foreach (var ForbiddenGoal in Goal.ForbiddenGoals)
                            {
                                if (MainList[i].GoalDesc == ForbiddenGoal)
                                {
                                    valid = false;
                                    problematicGoalIndex = i;
#if DEBUG
                                    LastResetReason += "ExistingIsForbiddenGoal";
#endif
                                    goto NameCheckingLoopDone; // Break the name checking loop
                                }
                            }
                            // Check if this goal is forbidden by the previously placed goal.
                            foreach (var ForbiddenGoal in MainList[i].ForbiddenGoals)
                            {
                                if (Goal.GoalDesc == ForbiddenGoal)
                                {
                                    valid = false;
                                    problematicGoalIndex = i;
#if DEBUG
                                    LastResetReason += "ForbiddenByExisting";
#endif
                                    goto NameCheckingLoopDone; // Break the name checking loop
                                }
                            }
                        }
                    }
                NameCheckingLoopDone:

                    if(!NoCategoryLogic) // Skip this check if category logic is turned off
                    {
                        // Put the current goal on the goal checking grid, a 2D array specifically for the CheckGrid function.
                        GoalCheckingGrid[index / 5, index % 5] = Goal;

                        // Check if all the goal's categories are non-exclusive with the rest of the grid.
                        // If not the goal will be invalid and the problematicGoalIndex will keep track of
                        // which index it conflicted with. If after we've looped through the PossibleGoals
                        // array there were no valid goals we ban the goal that was in the problematicGoalIndex
                        // and restart the goal choosing loop from index one.
                        if (valid)
                        {
                            valid = GridChecker.CheckGridGoals(GoalCheckingGrid, index, ref problematicGoalIndex);
#if DEBUG
                            if (!valid)
                                LastResetReason += "CC" + problematicGoalIndex + "";
#endif
                        }
                    }

                    if (valid)// If a goal is valid
                    {
                        MainList[index] = Goal;

                        if (NoTierLogic && Goal.Tier == -1 && !Goal.PossibleTiers.Contains(tier))
                            tier = Goal.PossibleTiers.ElementAt(BingoRand.Next() % Goal.PossibleTiers.Count());
                        else if (NoTierLogic && Goal.Tier > -1 && tier != Goal.Tier)
                            tier = Goal.Tier;

                        MainList[index].Tier = tier; // We also set the tier incase the goal had a tier range, this way we know how to define those goals later.

                        break; // Break the PossibleGoals loop
                    }
                }

                if (!valid) // If the category-and-tier-specific goal list didn't provide a valid goal after checking each.
                {
                    if (problematicGoalIndex != -1)
                    {
                        // If there was a problematicGoalIndex we ban it and just go back to the start of goal selection
                        if (!BannedGoalsForSlots.ContainsKey(problematicGoalIndex))
                            BannedGoalsForSlots[problematicGoalIndex] = new List<string>();

                        BannedGoalsForSlots[problematicGoalIndex].Add(MainList[problematicGoalIndex].GoalDesc);
#if DEBUG
                        string categoriesString = "";
                        foreach(var cat in MainList[problematicGoalIndex].GoalCategories)
                            categoriesString += cat.Name + "+";
                        LastResetReason += "!" + index + "isBanning" + problematicGoalIndex + "->" + categoriesString + "!";
#endif
                        goto StartOfGoalSelection;
                    }

                    if(NoTierLogic)
                        goto StartOfGoalSelection;

                    // If no goals in the category-and-tier-specific list are valid and there is nothing to ban we go back to tiergrid checking.
                    MainList = OldMainList;
#if DEBUG
                    LastResetReason += " | ";
#endif
                    if (UseFasterTierGridChecking)
                        goto FasterTierGridCheckingIterationSpot;
                    else
                        goto SlowerTierGridCheckingIterationSpot;
                }
            }

            if (DebugPrints)
            {
                if (NoTierLogic)
                {
                    int total = 0;
                    
                    finalTierGrid = new int[5, 5];
                    for (int i = 0; i < 5; i++)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            finalTierGrid[i, j] = MainList[(i * 5) + j].Tier;
                            total += MainList[(i * 5) + j].Tier;

                            //MainList[(i * 5) + j].GoalDesc += " - " + MainList[(i * 5) + j].Tier;     // For showing tier in goal desc
                        }
                    }
                    Trace.WriteLine("Average Tier: " + (total / 25.0f) );
                }

                PrintGrid(finalTierGrid);
            }
                


//#if DEBUG   // Save copies of the categories of the generation, this is for tallying.
            if (TallyMode)
            {
                GeneratedCategories = new List<GoalCategory>();
                foreach (var entry in OldMainList)
                {
                    GeneratedCategories.Add(entry.GoalCategories[0]);
                }

                FinalCategories = new List<GoalCategory>();
                foreach (var entry in MainList)
                {
                    foreach (var category in entry.GoalCategories)
                    {
                        FinalCategories.Add(category);
                    }
                }

                SlotsPossibleGoals.Clear();
                for (int i = 0; i < 25; i++)
                {
                    SlotsPossibleGoals.Add(new List<Goal>());
                    for (int u = 0; u < AllPossibleGoals[i].Count; u++)
                    {
                        SlotsPossibleGoals[i].Add(new Goal(AllPossibleGoals[i][u]));
                        SlotsPossibleGoals[i][u].Tier = MainList[i].Tier;
                    }
                }
            }
//#endif
            return true;
        }

#if DEBUG // The following DEBUG-only block are functions for generating and baking tier grids
        public static List<int[,]> GenerateTierGrids(bool keepFivesUnchanged)
        {
            List<int[,]> TierGrids = new List<int[,]>
            {
                new int[5, 5]     // Base 1
                {
                    { 1, 3, 4, 5, 2 },
                    { 5, 2, 1, 3, 4 },
                    { 3, 4, 5, 2, 1 },
                    { 2, 1, 3, 4, 5 },
                    { 4, 5, 2, 1, 3 },
                },
                new int[5, 5]     // Base 2
                {
                    { 1, 3, 4, 5, 2 },
                    { 2, 4, 1, 3, 5 },
                    { 3, 2, 5, 4, 1 },
                    { 5, 1, 3, 2, 4 },
                    { 4, 5, 2, 1, 3 },
                },
                new int[5, 5]     // Base 3
                {
                    { 1, 5, 4, 3, 2 },
                    { 2, 4, 3, 1, 5 },
                    { 3, 2, 5, 4, 1 },
                    { 5, 3, 1, 2, 4 },
                    { 4, 1, 2, 5, 3 },
                }
            };

            GT[] GridTransformations = new GT[39] { GT.rotCW, GT.rotCW, GT.rotCW, GT.flipH, GT.rotCW, GT.rotCW, GT.rotCW, GT.permute,
                                                    GT.rotCW, GT.rotCW, GT.rotCW, GT.flipH, GT.rotCW, GT.rotCW, GT.rotCW, GT.permute,
                                                    GT.rotCW, GT.rotCW, GT.rotCW, GT.flipH, GT.rotCW, GT.rotCW, GT.rotCW, GT.permute,
                                                    GT.rotCW, GT.rotCW, GT.rotCW, GT.flipH, GT.rotCW, GT.rotCW, GT.rotCW, GT.permute,
                                                    GT.rotCW, GT.rotCW, GT.rotCW, GT.flipH, GT.rotCW, GT.rotCW, GT.rotCW  };

            if (TiersPerLine != 15)
                TierGrids = GenerateGridsWithTargetSum(TierGrids, TiersPerLine, keepFivesUnchanged);

            List<int[,]> FinalTierGrids = new List<int[,]>();
            int[,] TierGrid;

            for (int i = 0 ; i < TierGrids.Count ; i++ )
            {
                TierGrid = TierGrids[i];
                FinalTierGrids.Add(TierGrid.Clone() as int[,]);

                for (int t = 0; t < GridTransformations.Count(); t++)
                {
                    switch (GridTransformations[t])
                    {
                        case GT.flipH:
                            FlipGridH(TierGrid); break;
                        case GT.rotCW:
                            RotateGridCW(TierGrid); break;
                        case GT.permute:
                            if (TiersPerLine == 15)
                                PermuteTiers(TierGrid);
                            else
                                t = GridTransformations.Count(); // Skip all remaining transforms if its not a pure 15 grid
                            break;
                    }

                    if(t < GridTransformations.Count())
                        FinalTierGrids.Add(TierGrid.Clone() as int[,]);
                }
            }
            
            for (int i = 0; i < FinalTierGrids.Count; i++)
            {
                for (int j = i + 1; j < FinalTierGrids.Count; j++)
                {
                    bool areEqual = true;

                    // Compare grid[i] with grid[j]
                    for (int row = 0; row < 5; row++)
                    {
                        for (int col = 0; col < 5; col++)
                        {
                            if (FinalTierGrids[i][row, col] != FinalTierGrids[j][row, col])
                            {
                                areEqual = false;
                                break;
                            }
                        }
                        if (!areEqual) break;
                    }

                    if (areEqual)
                    {
                        //Console.WriteLine($"Duplicate grids found at index {i} and {j}.");
                        FinalTierGrids.RemoveAt(j);
                        j--;
                    }
                }
            }
            
            return FinalTierGrids;
        }
        public static List<int[,]> GenerateGridsWithTargetSum(List<int[,]> tierGrids, int targetSum, bool keepFivesUnchanged)
        {
            // Validate the target sum
            if (targetSum < 9 || targetSum > 15)
                throw new ArgumentOutOfRangeException(nameof(targetSum), "Target sum must be between 9 and 15");

            List<int[,]> result = new List<int[,]>();

            foreach (var grid in tierGrids)
            {
                List<int[,]> possibleGrids = new List<int[,]>();
                AdjustGridToTargetSum(grid, new int[5, 5], 0, targetSum, possibleGrids, keepFivesUnchanged);
                result.AddRange(possibleGrids);
            }

            return result;
        }

        private static void AdjustGridToTargetSum(int[,] originalGrid, int[,] currentGrid, int index, int targetSum, List<int[,]> result, bool keepFivesUnchanged)
        {
            if (index == 25)
            {
                if (AllLinesSumToTarget(currentGrid, targetSum))
                    result.Add((int[,])currentGrid.Clone());
                return;
            }

            int row = index / 5;
            int col = index % 5;

            int originalValue = originalGrid[row, col];
            int maxAllowedValue = keepFivesUnchanged && originalValue == 5 ? 5 : Math.Min(originalValue, targetSum - 4);

            for (int i = 1; i <= maxAllowedValue; i++)
            {
                if (keepFivesUnchanged && originalValue == 5 && i != 5)
                    continue;

                currentGrid[row, col] = i;
                AdjustGridToTargetSum(originalGrid, currentGrid, index + 1, targetSum, result, keepFivesUnchanged);
            }
        }

        private static bool AllLinesSumToTarget(int[,] grid, int targetSum)
        {
            for (int i = 0; i < 5; i++)
            {
                if (GetRowSum(grid, i) != targetSum || GetColumnSum(grid, i) != targetSum)
                    return false;
            }

            if (GetDiagonalSum(grid, true) != targetSum || GetDiagonalSum(grid, false) != targetSum)
                return false;

            return true;
        }

        private static int GetRowSum(int[,] grid, int row)
        {
            int sum = 0;
            for (int col = 0; col < 5; col++)
            {
                sum += grid[row, col];
            }
            return sum;
        }

        private static int GetColumnSum(int[,] grid, int col)
        {
            int sum = 0;
            for (int row = 0; row < 5; row++)
            {
                sum += grid[row, col];
            }
            return sum;
        }

        private static int GetDiagonalSum(int[,] grid, bool mainDiagonal)
        {
            int sum = 0;
            if (mainDiagonal)
            {
                for (int i = 0; i < 5; i++)
                {
                    sum += grid[i, i];
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    sum += grid[i, 4 - i];
                }
            }
            return sum;
        }
        public static int ValidateGrid(int[,] grid)
        {
            int targetSum = GetRowSum(grid, 0);

            // Check all rows
            for (int i = 1; i < 5; i++)
            {
                if (GetRowSum(grid, i) != targetSum)
                    throw new Exception($"Row {i + 1} does not add up to {targetSum}.");
            }

            // Check all columns
            for (int i = 0; i < 5; i++)
            {
                if (GetColumnSum(grid, i) != targetSum)
                    throw new Exception($"Column {i + 1} does not add up to {targetSum}.");
            }

            // Check the main diagonal
            if (GetDiagonalSum(grid, true) != targetSum)
                throw new Exception($"Main diagonal does not add up to {targetSum}.");

            // Check the anti-diagonal
            if (GetDiagonalSum(grid, false) != targetSum)
                throw new Exception($"Anti-diagonal does not add up to {targetSum}.");

            // If all checks pass, return the target sum
            return targetSum;
        }

        public static void BakeAndSaveTierGridsToJson(string jsonFilePath)
        {
            Dictionary<int, List<int[,]>> resultsWithFalse = new Dictionary<int, List<int[,]>>();
            Dictionary<int, List<int[,]>> resultsWithTrue = new Dictionary<int, List<int[,]>>();

            // Vary TiersPerLine from 10 to 15 and calculate for keepFales and keepTrue
            for (int tiers = 9; tiers <= 15; tiers++)
            {
                TiersPerLine = tiers;
                bool keepFivesUnchanged = false;
                resultsWithFalse[tiers] = GenerateTierGrids(keepFivesUnchanged);

                keepFivesUnchanged = true;
                resultsWithTrue[tiers] = GenerateTierGrids(keepFivesUnchanged);

                Console.WriteLine($"{tiers} done.");
            }

            string jsonData = JsonConvert.SerializeObject(new { ResultsWithFalse = resultsWithFalse, ResultsWithTrue = resultsWithTrue });
            File.WriteAllText(jsonFilePath, jsonData);
        }
#endif
        public static List<int[,]> BalanceGrids(List<int[,]> grids, double temperature, bool keepFivesUnchanged)
        {
            // Step 1: Calculate the global frequency of each number
            int[] globalDistribution = new int[6]; // Index 0 is unused, 1-5 for numbers
            int totalNumbers = grids.Count * 5 * 5;

            foreach (var grid in grids)
            {
                int[] gridDistribution = GetGridDistribution(grid, keepFivesUnchanged);
                for (int i = 1; i <= 5; i++)
                {
                    globalDistribution[i] += gridDistribution[i];
                }
            }

            // Step 2: Calculate the global average for each number
            double[] globalAverage = new double[6];
            for (int i = 1; i <= 5; i++)
            {
                if (!(keepFivesUnchanged && i == 5))
                {
                    globalAverage[i] = (double)globalDistribution[i] / grids.Count;
                }
            }

            // Step 3: Calculate skew for each grid based on its contribution to the global average
            List<Tuple<int[,], double>> gridScores = new List<Tuple<int[,], double>>();

            foreach (var grid in grids)
            {
                int[] gridDistribution = GetGridDistribution(grid, keepFivesUnchanged);
                double score = CalculateEvenDistributionScore(gridDistribution);
                score += CalculateGridSkewScore(gridDistribution, globalDistribution, globalAverage, keepFivesUnchanged);
                gridScores.Add(new Tuple<int[,], double>(grid, score));
            }

            gridScores = gridScores.OrderByDescending(x => x.Item2).ToList();
            gridScores.Reverse();

            // Find the minimum and maximum scores
            double minScore = gridScores.Min(pair => pair.Item2);
            double maxScore = gridScores.Max(pair => pair.Item2);

            // Calculate the score cutoff based on the temperature
            double scoreCutoff = minScore + temperature * (maxScore - minScore);

            // Select grids with scores greater than or equal to the cutoff score
            var filteredGrids = gridScores.Where(pair => pair.Item2 >= scoreCutoff)
                                          .Select(pair => pair.Item1)  // Only take the grid from the tuple
                                          .ToList();

            return filteredGrids;
        }

        private static int[] GetGridDistribution(int[,] grid, bool keepFivesUnchanged)
        {
            int[] distribution = new int[6]; // Index 0 is unused, 1-5 for numbers
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    int number = grid[row, col];
                    if (keepFivesUnchanged && number == 5) continue;
                    distribution[number]++;
                }
            }
            return distribution;
        }

        private static double CalculateGridSkewScore(int[] gridDistribution, int[] globalDistribution, double[] globalAverage, bool keepFivesUnchanged)
        {
            double score = 0.0;
            for (int i = 1; i <= (keepFivesUnchanged ? 4 : 5); i++)
            {
                if (globalAverage[i] > 0)  // Avoid division by zero
                {
                    // Calculate the deviation of the grid's distribution from the global average
                    double deviation = gridDistribution[i] - globalAverage[i];

                    if (gridDistribution[i] > globalAverage[i])
                    {
                        // Overrepresented in the grid: penalize more heavily by subtracting squared deviation
                        score -= Math.Pow(deviation, 2);
                    }
                    else
                    {
                        // Underrepresented in the grid: reward by adding squared deviation
                        score += Math.Pow(deviation, 2);
                    }
                }
            }
            return score;
        }

        private static double CalculateEvenDistributionScore(int[] gridDistribution)
        {
            // Calculate the mean (average) number of occurrences for the numbers 1 to 5
            double mean = gridDistribution.Average();

            double variance = 0.0;

            // Calculate the variance: sum of squared differences from the mean
            for (int i = 1; i <= 5; i++)
            {
                variance += Math.Pow(gridDistribution[i] - mean, 2);
            }

            // Return the inverse of the variance for scoring: higher score means more even distribution
            // Adding a small constant (1e-5) to avoid division by zero in case variance is exactly zero
            return 1000.0 / (variance + 1e-5);
        }

        public static (Dictionary<int, List<int[,]>>, Dictionary<int, List<int[,]>>) LoadTierGridsFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException("JSON file not found.", jsonFilePath);
            }

            string jsonData = File.ReadAllText(jsonFilePath);
            var data = JsonConvert.DeserializeAnonymousType(jsonData, new { ResultsWithFalse = default(Dictionary<int, List<int[,]>>), ResultsWithTrue = default(Dictionary<int, List<int[,]>>) });

            return (data.ResultsWithFalse, data.ResultsWithTrue);
        }

        /// <summary>
        /// Chooses the specifics of the goals, such as goals with variable amounts, regions and challenges. Also changes other types of dynamic goal names.
        /// </summary>
        public void GenerateGoals()
        {
            // Generate the final goals
            foreach (Goal Goal in MainList)
            {
                bool variableAmount = Goal.GoalDesc.Contains("|X|");
                bool variableRegion = Goal.GoalDesc.Contains("|Region|");
                bool variableAttrib = Goal.GoalDesc.Contains("|Attribute1|");

                int TargetTier = Goal.Tier - Goal.ModifierChangedAmount;

                if (variableAmount && variableRegion) // We could consider making these functions non mutually exclusive but this would involve choosing
                {                                     // a valid goal outside of the functions, which would take a restructuring.
                    if (Goal.AmountStep != -1)
                        CalculateGoalAmountAndRegion(Goal, TargetTier);
                }
                else if (variableAmount)
                {
                    if (Goal.AmountStep != -1)
                        CalculateGoalAmount(Goal, TargetTier);
                }
                else if (variableRegion)
                    CalculateRegion(Goal, TargetTier);
                else if (variableAttrib)
                    GenerateAttributeGoal(Goal, TargetTier);
                
                if (Goal.AssignedChallengeGoal)
                    GenerateChallengeGoal(Goal, TargetTier);

                // Location appending
                switch (Globals.LocationDetailLevel)
                {
                    case 0:
                        if (Goal.LocationLevels[0] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[0] + ")";
                        break;
                    case 1:
                        if (Goal.LocationLevels[1] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[1] + ")";
                        else if (Goal.LocationLevels[0] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[0] + ")";
                        break;
                    case 2:
                        if (Goal.LocationLevels[2] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[2] + ")";
                        else if (Goal.LocationLevels[1] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[1] + ")";
                        else if (Goal.LocationLevels[0] != "None")
                            Goal.GoalDesc += " (" + Goal.LocationLevels[0] + ")";
                        break;
                    default:
                        break;
                }
            }
        }

        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        //		Misc. Functions
        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        public static void ParseGoalCategoryListJSON(ref Dictionary<string, GoalCategory> Categories, ref List<string> AnyCategories, ref Dictionary<int, string> CategoryNamesByID)
        {
            GoalCategory.CategoryIDCounter = 3;

            if (Categories == null)
                Categories = new Dictionary<string, GoalCategory>();
            else if (Categories.Count > 0)
                return;                   // I changed it to not re-read the list if it has done it once for some tally optimization

            JArray BaseGoalsJArray = JArray.Parse(File.ReadAllText(Globals.DataFolderPathDev + @"GoalCategoryList.json"));

            // softmax test -> List<GoalCategory> CategoriesLocal = new List<GoalCategory>();

            foreach (var JObject in BaseGoalsJArray)
            {
                GoalCategory CurrentCategory = new GoalCategory(
                    JObject["Name"].ToObject<string>(),
                    JObject["Chance"].ToObject<int>(),
                    false, // IsChallengeOnly is set when generating categories
                    JObject["SelfExclusive"].ToObject<bool>(),
                    JObject["CanReplaceAny"].ToObject<bool>(),
                    JObject["ID"].ToObject<int>(),
                    JObject["MaxPerBoard"].ToObject<int>()
                    );

                foreach (var JObjectEB in JObject["ExclusivityBuddies"])
                {
                    CurrentCategory.ExclusivityBuddies.Add(JObjectEB.ToObject<string>());
                }

                Categories.Add(JObject["Name"].ToObject<string>(), CurrentCategory);
                CategoryNamesByID[CurrentCategory.ID] = CurrentCategory.Name;
                // softmax test -> CategoriesLocal.Add(CurrentCategory);
            }

            // softmax test -> List<double> softMax = CategorySoftmax(CategoriesLocal, 35);

            foreach (var Category in Categories)
            {
                // Add categories that can replace "any" into a list
                if (Category.Value.CanReplaceAny)
                    AnyCategories.Add(Category.Value.Name);
            }

            // softmax test ->  then use U.ChooseFromChances0to1


            foreach (var Category in Categories)
            {
                foreach (var EB in Category.Value.ExclusivityBuddies)
                {
                    // If there are some one-way ExclusivityBuddies, make them two-way.

                    if (!Categories[EB].ExclusivityBuddies.Contains(Category.Key))
                    {
                        GoalCategory tempGC = Categories[EB];
                        tempGC.ExclusivityBuddies.Add(Category.Key);
                        Categories[EB] = tempGC;
                    }
                }
            }
        }

        // Reads GoalList.json and put all the data in FullBaseGoalList
        public static void ParseGoalListJSON(ref List<Goal> FullBaseGoalList, ref Dictionary<string, GoalCategory> Categories, int StartingArea, uint BoardsGenerated = 0)
        {
            FullBaseGoalList = new List<Goal>();

            JArray BaseGoalsJArray = JArray.Parse(File.ReadAllText(Globals.DataFolderPathDev + @"GoalList.json"));

            foreach (JObject JObject in BaseGoalsJArray)
            {
                bool DontAddGoal = false;
                string name = JObject["Description"].ToObject<string>();
                List<int> PossibleTiers = new List<int>();
                List<GoalCategory> GoalCategories = new List<GoalCategory>();
                List<HintDesc> HintDescs = new List<HintDesc>();
                List<string> ForbiddenGoals = new List<string>();
                Dictionary<string, int> ModifierTierChanges = new Dictionary<string, int>();
                int MinimumRegions = -1;
                bool MinimumRegionsIncludeLoS = false;

                if (NoHitless && name.Contains("Fragile Tear"))
                    continue;

                // Exclude "Make a ring with 5 Rainbow Stones" if crafting recipes are randomized
                if (Globals.CraftingRecipesRandomized && name.Contains("Rainbow Stones"))
                    continue;

                if (!Globals.ModifiersEnabled && JObject["RequiredModifiers"].Count() > 0)
                    continue;

                if (Globals.ModifiersEnabled && JObject["RequiredModifiers"].Count() > 0)
                {
                    // If modifiers are active and this goal requires a modifier.

                    foreach (var JObjectRequiredModifier in JObject["RequiredModifiers"])
                    {
                        // Loop through each required modifier.

                        bool HasRequiredModifier = false;

                        for (int i = 0; i < Globals.CurrentModifierCount && !HasRequiredModifier; i++)
                        {
                            // Loop through each active modifier.

                            if (Globals.CurrentModifierList[i].Name == JObjectRequiredModifier.ToObject<string>())
                            {
                                // If a active modifier matches the currently checked required modifier.
                                HasRequiredModifier = true;
                            }
                        }

                        if (!HasRequiredModifier)
                        {
                            // If no active modifier matches the currently checked required modifier.
                            DontAddGoal = true;
                            break; // Stop checking the required modifiers
                        }
                    }
                }

                if (DontAddGoal)
                    continue;

                foreach (var JObjectGoalCategory in JObject["Categories"])
                {
                    string categoryString = JObjectGoalCategory.ToObject<string>();

                    if (NoEvil && categoryString == "Evil")
                    {
                        DontAddGoal = true;
                        break;
                    }

                    if (Categories.ContainsKey(categoryString))
                        GoalCategories.Add(Categories[categoryString]);
                    else
                    {
                        Categories.Add(categoryString, new GoalCategory(categoryString)); // <- Default values (tag)
                        GoalCategories.Add(Categories[categoryString]);
#if DEBUG
                        DialogResult dr = MessageBox.Show("A category exists in GoalList but not in GoalCategoryList.json.\nCurrently it will get added with default variables, which means it will be treated like a tag, so make it a tag instead.\nThe category is: " + categoryString + "\n(This message is not shown in final release.)", "Warning! Undefined category!");
#endif
                    }
                }

                if (JObject.ContainsKey("Tags")) {
                    foreach (var JObjectGoalCategory in JObject["Tags"])
                    {
                        string categoryString = JObjectGoalCategory.ToObject<string>();

                        if (categoryString == "WeaponCollection")
                        {
                            if (!Globals.WeaponCollectionGoalsAllowed)
                            {
                                DontAddGoal = true;
                                break;
                            }
                            else if (name.Contains("Hammers") && Globals.WeaponsReplacedAmount(WeaponType.Hammer) + Globals.WeaponsReplacedAmount(WeaponType.GreatHammer) > 2)
                            {
                                DontAddGoal = true;
                                break;
                            }
                            else if (name.Contains("Axes") && Globals.WeaponsReplacedAmount(WeaponType.Axe) + Globals.WeaponsReplacedAmount(WeaponType.Greataxe) > 2)
                            {
                                DontAddGoal = true;
                                break;
                            }
                        }

                        if (categoryString == "SidearmCollection")
                        {
                            if (!Globals.SidearmCollectionGoalsAllowed)
                            {
                                DontAddGoal = true;
                                break;
                            }
                            else if (name.Contains("Staves") && Globals.WeaponsReplacedAmount(WeaponType.Staff) > 0)
                            {
                                DontAddGoal = true;
                                break;
                            }
                            else if (name.Contains("Seals") && Globals.WeaponsReplacedAmount(WeaponType.Seal) > 0)
                            {
                                DontAddGoal = true;
                                break;
                            }
                            else if (name.Contains("Shields") && Globals.WeaponsReplacedAmount(WeaponType.SmallShield) +
                                Globals.WeaponsReplacedAmount(WeaponType.MediumShield) +
                                Globals.WeaponsReplacedAmount(WeaponType.Greatshield) > 4)
                            {
                                DontAddGoal = true;
                                break;
                            }
                        }

                        if (!Categories.ContainsKey(categoryString))
                            Categories.Add(categoryString, new GoalCategory(categoryString)); // <- Default values (tag)
                        GoalCategories.Add(Categories[categoryString]);
                    }
                }

                if (DontAddGoal)
                    continue;

                foreach (var JObjectForbiddenStartingRegions in JObject["ForbiddenStartingRegions"])
                {
                    if (JObjectForbiddenStartingRegions.ToObject<int>() == StartingArea)
                        DontAddGoal = true;
                }

                if (DontAddGoal)
                    continue;

                foreach (var JObjectTier in JObject["TierRange"])
                {
                    PossibleTiers.Add(JObjectTier.ToObject<int>());
                }

                foreach (JObject JObjectHintDesc in JObject["HintDesc"])
                {
                    HintDescs.Add(new HintDesc(JObjectHintDesc, "Goal Name", name));
                }

                foreach (var JObjectForbiddenGoal in JObject["ForbiddenGoals"])
                    ForbiddenGoals.Add(JObjectForbiddenGoal.ToObject<string>());

                foreach (var JObjectModifierTierChange in JObject["ModifierTierChanges"])
                    ModifierTierChanges.Add(JObjectModifierTierChange["Modifier"].ToObject<string>(),
                                            JObjectModifierTierChange["TierChange"].ToObject<int>());

                //if (Goal.GoalCategories.Count() > 1)  // This needs to only apply for every category that can replace any TODO: add it to sortGoals
                //    BonusProbability -= (12 * Goal.GoalCategories.Count());

                int BonusProbability = JObject["BonusProbability"].ToObject<int>();

                if (NoTierLogic && GoalCategories.Count > 1)
                {
                    int NrOfCanReplaceAny = 0;
                    foreach (var Cat in GoalCategories)
                        if (Cat.CanReplaceAny)
                            NrOfCanReplaceAny++;

                    if(NrOfCanReplaceAny > 1)
                        BonusProbability -= (30 * (NrOfCanReplaceAny-1));
                }

                if (JObject.ContainsKey("MinimumRegions")){ MinimumRegions = JObject["MinimumRegions"].ToObject<int>(); }
                if (JObject.ContainsKey("MinimumRegionsIncludeLoS")){ MinimumRegionsIncludeLoS = JObject["MinimumRegionsIncludeLoS"].ToObject<bool>(); }

                List<string> LocationLevels = new List<string>();
                if (JObject.ContainsKey("LocationBasic")) { LocationLevels.Add(JObject["LocationBasic"].ToObject<string>()); }
                if (JObject.ContainsKey("LocationGeneral")) { LocationLevels.Add(JObject["LocationGeneral"].ToObject<string>()); }
                if (JObject.ContainsKey("LocationDetailed")) { LocationLevels.Add(JObject["LocationDetailed"].ToObject<string>()); }


                FullBaseGoalList.Add(new Goal(
                name,
                LocationLevels,
                GoalCategories,
                HintDescs,
                PossibleTiers,
                ForbiddenGoals,
                ModifierTierChanges,
                JObject["ChallengeGoal"].ToObject<string>(),
                JObject["ChallengeRange"].ToObject<int>(),
                JObject["Tier"].ToObject<int>(),
                BonusProbability,
                JObject["Region"].ToObject<int>(),
                JObject["AmountMin"].ToObject<int>(),
                JObject["AmountMax"].ToObject<int>(),
                JObject["AmountStep"].ToObject<int>(),
                JObject["AmountPerTier"].ToObject<int>(),
                JObject["AmountExtraTierBreakpoint"].ToObject<int>(),
                MinimumRegions,
                MinimumRegionsIncludeLoS
                ));
            }
        }
        /// <summary>
        /// Returns a list of all goals that exist in their pure form in the json file GoalList.json, this is made to be used from outside the
        /// BingoGenerator class, currently only in tally calculation. Not optimized so don't over do it.
        /// </summary>
        public static List<Goal> GetFullGoalList()
        {
            Dictionary<string, GoalCategory> TempCategories = new Dictionary<string, GoalCategory>();
            List<string> TempAnyCategories = new List<string>();
            Dictionary<int, string> TempCategoryNamesByID = new Dictionary<int, string>();

            ParseGoalCategoryListJSON(ref TempCategories, ref TempAnyCategories, ref TempCategoryNamesByID);

            List<Goal> FullNewGoalList = new List<Goal>();
            ParseGoalListJSON(ref FullNewGoalList, ref TempCategories, -1);
            return FullNewGoalList;
        }

        /// <summary>
        /// Returns a list of all categories that exist in their pure form in the json file GoalCategoryList.json, this is made to be used from outside the
        /// BingoGenerator class, currently only in tally calculation. Not optimized so don't over do it.
        /// </summary>
        public static List<GoalCategory> GetFullCategoryList()
        {
            Dictionary<string, GoalCategory> TempResult = new Dictionary<string, GoalCategory>();
            List<string> TempAnyCategories = new List<string>();
            Dictionary<int, string> TempCategoryNamesByID = new Dictionary<int, string>();

            ParseGoalCategoryListJSON(ref TempResult, ref TempAnyCategories, ref TempCategoryNamesByID);

            List<GoalCategory> result = new List<GoalCategory>();

            foreach (GoalCategory category in TempResult.Values)
                result.Add(category);

            return result;
        }

        private List<Goal> GetSortedGoalList(int CategoryID, int IsChallengeOnly, int Tier)
        {
            if (SortedGoalsLimited[CategoryID][IsChallengeOnly][Tier].Count > 0)
                return SortedGoalsLimited[CategoryID][IsChallengeOnly][Tier];
            else
                return SortedGoals[CategoryID][IsChallengeOnly][Tier];
        }

        private List<Goal> GetSortedGoalsSubset(ref List<List<List<List<Goal>>>> SortedGoals, int CategoryID)
        {
            List<Goal> result = new List<Goal>();
            foreach (var subset in SortedGoals[CategoryID][0]) // For now I only do non-full challenge becuse i don't know how to handle that.
            {
                result.AddRange(subset);
            }
            return result;
        }

        private int GetSortedGoalsSubsetCount(ref List<List<List<List<Goal>>>> SortedGoals, int CategoryID, bool includeChallengeGoals = false)
        {
            int result = 0;
            foreach (var subset in SortedGoals[CategoryID][0]) // For now I only do non-full challenge becuse i don't know how to handle that.
            {                                                  // (For main category picking)
                result += subset.Count;
            }
            if(includeChallengeGoals)
            {
                foreach (var subset in SortedGoals[CategoryID][1])
                {
                    result += subset.Count;
                }
            }
            return result;
        }

        public void ParseChallengeJSON()
        {
            if (FullChallengeModifierList.Count > 0)
                return;  // I changed it to not re-read the list if it has done it once for some tally optimization

            JArray ChallengesJArray = JArray.Parse(File.ReadAllText(Globals.DataFolderPathDev + @"ChallengesList.json"));

            foreach (JObject JChallenge in ChallengesJArray)
            {
                bool DontAddChallenge = false;
                string Name = JChallenge["Description"].ToObject<string>();

                if (NoHitless && Name.Contains("Fragile Tear"))
                    continue;

                if (Globals.ModifiersEnabled && JChallenge["RequiredModifiers"].Count() > 0)
                {
                    // If modifiers are active and this Challenge requires a modifier.

                    foreach (var JObjectRequiredModifier in JChallenge["RequiredModifiers"])
                    {
                        // Loop through each required modifier.

                        bool HasRequiredModifier = false;

                        for (int i = 0; i < Globals.CurrentModifierCount && !HasRequiredModifier; i++)
                        {
                            // Loop through each active modifier.

                            if (Globals.CurrentModifierList[i].Name == JObjectRequiredModifier.ToObject<string>())
                            {
                                // If a active modifier matches the currently checked required modifier.
                                HasRequiredModifier = true;
                            }
                        }

                        if (!HasRequiredModifier)
                        {
                            // If no active modifier matches the currently checked required modifier.
                            DontAddChallenge = true;
                            break; // Stop checking the required modifiers
                        }
                    }
                }

                // Check WeaponsReplacedAmount and if it should exclude the challenge

                if(Globals.HasReplacedAnyWeapons())
                {
                    if (Name.Contains("Bows/Crossbows only") && Globals.WeaponsReplacedAmount(WeaponType.Bow) + Globals.WeaponsReplacedAmount(WeaponType.Crossbow) > 3)
                    {
                        DontAddChallenge = true;
                        if (!Globals.SidearmCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                    else if (Name.Contains("with a crossbow bolt") && Globals.WeaponsReplacedAmount(WeaponType.Crossbow) > 2)
                    {
                        DontAddChallenge = true;
                        if (!Globals.SidearmCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                    else if (Name.Contains("Claws/Fists/Whips only") && Globals.WeaponsReplacedAmount(WeaponType.Claw) + Globals.WeaponsReplacedAmount(WeaponType.Fist) + Globals.WeaponsReplacedAmount(WeaponType.Whip) > 4)
                    {
                        DontAddChallenge = true;
                        if (!Globals.WeaponCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                    else if (Name.Contains("Flails/Twinblades only") && Globals.WeaponsReplacedAmount(WeaponType.Flail) + Globals.WeaponsReplacedAmount(WeaponType.Twinblade) > 3)
                    {
                        DontAddChallenge = true;
                        if (!Globals.WeaponCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                    else if (Name.Contains("Torch only") && Globals.WeaponsReplacedAmount(WeaponType.Torch) > 2)
                    {
                        DontAddChallenge = true;
                        if (!Globals.SidearmCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                    else if (Name.Contains("only shield attacks") &&
                        Globals.WeaponsReplacedAmount(WeaponType.SmallShield) +
                        Globals.WeaponsReplacedAmount(WeaponType.MediumShield) +
                        Globals.WeaponsReplacedAmount(WeaponType.Greatshield) > 5)
                    {
                        DontAddChallenge = true;
                        if (!Globals.SidearmCollectionGoalsAllowed) { DontAddChallenge = true; }
                    }
                }

                if (DontAddChallenge)
                    continue;

                List<string> ExcludedBossPools = new List<string>();
                List<string> ForbiddenGoals = new List<string>();
                Dictionary<string, int> ModifierTierChanges = new Dictionary<string, int>();
                List<HintDesc> HintDescs = new List<HintDesc>();

                foreach (var JObjectExcludedBossPools in JChallenge["ExcludedBossPools"])
                    ExcludedBossPools.Add(JObjectExcludedBossPools.ToObject<string>());

                foreach (var JObjectForbiddenGoal in JChallenge["ForbiddenGoals"])
                    ForbiddenGoals.Add(JObjectForbiddenGoal.ToObject<string>());

                foreach (var JObjectModifierTierChange in JChallenge["ModifierTierChanges"])
                    ModifierTierChanges.Add(JObjectModifierTierChange["Modifier"].ToObject<string>(),
                                            JObjectModifierTierChange["TierChange"].ToObject<int>());

                foreach (JObject JObjectHintDesc in JChallenge["HintDesc"])
                {
                    HintDescs.Add(new HintDesc(JObjectHintDesc, "Origin Name", "Goal Challenge: \"" + Name + "\""));
                }

                string SoloEligible = JChallenge["SoloEligible"].ToObject<string>();
                if (Globals.IsModifierActive("ForbiddenSummoning") || !Globals.SpiritAshesEnabled)
                    SoloEligible = "No";

                FullChallengeModifierList.Add(new ChallengeModifier(
                    Name,
                    ExcludedBossPools,
                    JChallenge["IsFinishGoal"].ToObject<bool>(),
                    JChallenge["EnemyChallengeEligible"].ToObject<bool>(),
                    JChallenge["BaseTierAdd"].ToObject<int>(),
                    JChallenge["ChallengeTier"].ToObject<int>(),
                    JChallenge["BonusProbability"].ToObject<int>(),
                    SoloEligible,
                    JChallenge["IsWeaponCollection"].ToObject<string>(),
                    ForbiddenGoals,
                    ModifierTierChanges,
                    HintDescs
                    ));
            }
        }

        public void ParseBoardDescJSON(string name, ref List<GoalCategory> GeneratedCategories)
        {
            JArray BoardDescJArray;

            if(BoardDescJArrays.ContainsKey(name))
                BoardDescJArray = BoardDescJArrays[name];
            else
            {
                BoardDescJArray = JArray.Parse(File.ReadAllText(Globals.DataFolderPathDev + name + ".json"));
                BoardDescJArrays[name] = BoardDescJArray;
            }

            ForbiddenAnyCategories.Clear();
            int index = 0;

            foreach (JArray Cell in BoardDescJArray)
            {
                List<int> Chances = new List<int>();
                List<string> CellCategories = new List<string>();
                List<bool> ChallengeOnly = new List<bool>();
                List<List<int>> RequiredTiers = new List<List<int>>();

                foreach (JObject Possibility in Cell)
                {
                    string CategoryName = Possibility["Category"].ToObject<string>();

                    if (Possibility.ContainsKey("PreventCategoryReplacingAny") && Possibility["PreventCategoryReplacingAny"].ToObject<bool>())
                    {
                        GoalCategory temp = Categories[CategoryName];
                        temp.CanReplaceAny = false;
                        Categories[CategoryName] = temp;
                        ForbiddenAnyCategories.Add(AnyCategories.IndexOf(CategoryName));
                        //AnyCategories.Remove(CategoryName); // Note: this is a permanent change, which we might not want in the future
                        //AnyCategoriesChances.Remove(CategoryName);
                    }

                    if (!Possibility.ContainsKey("Chance")) // No chance variable, therefore the cell is garanteed, we choose it, apply some things, and move on.
                    {
                        // Check if there are actually any goals in the single category
                        int CategoryGoalCount = GetSortedGoalsSubsetCount(ref SortedGoals, Categories[CategoryName].ID, true);
                        if (CategoryGoalCount <= 0 && CategoryName != "Any")
                            CategoryName = "Any"; // Change the cell to "Any" if there are no goals in the category

                        GeneratedCategories[index] = Categories[CategoryName];

                        if (Possibility.ContainsKey("ChallengeOnly") && Possibility["ChallengeOnly"].ToObject<bool>())
                        {
                            GoalCategory tempGC = Categories[CategoryName];
                            tempGC.IsChallengeOnly = Possibility["ChallengeOnly"].ToObject<bool>();
                            GeneratedCategories[index] = tempGC;
                        }
                        if (Possibility.ContainsKey("RequiredTiers") && Possibility["RequiredTiers"].ToObject<JArray>().Count > 0)
                        {
                            GoalCategory tempGC = Categories[CategoryName];
                            
                            foreach (var RequiredTier in Possibility["RequiredTiers"])
                                tempGC.RequiredTiers.Add(RequiredTier.ToObject<int>());

                            GeneratedCategories[index] = tempGC;
                        }
                        goto CellDone;
                    }
                    else // Else there are multiple possibilities, we need to save variables in arrays and use them later when something has been chosen.
                    {
                        if (Possibility.ContainsKey("ChallengeOnly") && Possibility["ChallengeOnly"].ToObject<bool>())
                            ChallengeOnly.Add(true);
                        else
                            ChallengeOnly.Add(false);

                        RequiredTiers.Add(new List<int>());
                        
                        if (Possibility.ContainsKey("RequiredTiers") && Possibility["RequiredTiers"].ToObject<JArray>().Count > 0)
                        {
                            foreach (var RequiredTier in Possibility["RequiredTiers"])
                                RequiredTiers[RequiredTiers.Count - 1].Add(RequiredTier.ToObject<int>());
                        }

                        int CategoryGoalCount = GetSortedGoalsSubsetCount(ref SortedGoals, Categories[CategoryName].ID, true);
                        if (CategoryGoalCount > 0 || CategoryName == "Any") // Check to see if there are any goals in the category
                        {
                            Chances.Add(Possibility["Chance"].ToObject<int>());
                            CellCategories.Add(CategoryName);
                        }
                        else
                        {
                            int delLater = 0;
                        }
                    }
                }

                if(Chances.Count < 1) // If there were supposed to be chances but none were valid.
                {
                    GeneratedCategories[index] = Categories["Any"];
                    goto CellDone;
                }

                var NormalizedChances = U.NormalizeValuesWithTemperature0to1(Chances);

                int ChosenPossibility = U.ChooseFromChances0to1(ref BingoRand, NormalizedChances);

                if (Cell[ChosenPossibility].ToObject<JObject>().ContainsKey("ChallengeOnly") || RequiredTiers[ChosenPossibility].Count > 0)
                {
                    GoalCategory GC = Categories[CellCategories[ChosenPossibility]];

                    GC.IsChallengeOnly = ChallengeOnly[ChosenPossibility];

                    GC.RequiredTiers = RequiredTiers[ChosenPossibility];

                    GeneratedCategories[index] = GC;
                }
                else
                    GeneratedCategories[index] = Categories[CellCategories[ChosenPossibility]];

                CellDone:
                index++;
            }

            for (int i = index; i < 25; i++)
            {
                GeneratedCategories[i] = Categories["Any"];
            }
        }

        // Called when the board is done, it writes two files. One with just the names of the goals in order of how they appear on the board.
        // And one with the hint descriptions, which is read by the Hint Generator. This includes the modifiers' hint descriptions
        public void WriteResultJSON()
        {
            JArray FinalGoalsJArray = new JArray();

            foreach (var Goal in MainList)
            {
                JObject Object = new JObject
                { { "name", Goal.GoalDesc } };

                FinalGoalsJArray.Add(Object);
            }

            using (StreamWriter file = File.CreateText(Globals.DataFolderPathMod + @"LastGeneratedGoals.json"))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                writer.Formatting = Formatting.Indented;
                FinalGoalsJArray.WriteTo(writer);
            }

            FinalGoalsJArray = new JArray();

            foreach (var Goal in MainList)
                foreach (var HintDesc in Goal.HintDescs)
                    FinalGoalsJArray.Add(HintDesc.getJObject());

            if (Globals.ModifiersEnabled)
                foreach (var Mod in Globals.CurrentModifierList)
                    foreach (var HintDesc in Mod.HintDescs)
                        FinalGoalsJArray.Add(HintDesc.getJObject());

            using (StreamWriter file = File.CreateText(Globals.DataFolderPathMod + @"LastGeneratedHintDescs.json"))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                writer.Formatting = Formatting.Indented;
                FinalGoalsJArray.WriteTo(writer);
            }
            
        }

        // Turns the finished board into a json string which can be copied into the clipboard and pasted into bingosync/other
        public string FinalGoalListString()
        {
            JArray FinalGoalsJArray = new JArray();

            foreach (var Goal in MainList)
            {
                JObject GoalObject = new JObject();
                GoalObject["name"] = Goal.GoalDesc;
                FinalGoalsJArray.Add(GoalObject);
            }

            FinalList = FinalGoalsJArray.ToString();

            return FinalList;
        }

        // Prints the grid to console
        public void PrintGrid<T>(List<T> Grid, int padding = 20)
        {
            Trace.WriteLine("----------------------------------------------------------------------------------------------------");
            for (int i = 0; i < 25; i++)
            {
                Trace.Write(Grid[i].ToString().PadRight(padding, ' '));

                if (i > 0 && i < 25 && (i + 1) % 5 == 0)
                {
                    Trace.WriteLine(Environment.NewLine);
                    Trace.WriteLine(Environment.NewLine);
                    Trace.WriteLine(Environment.NewLine);
                    Trace.WriteLine(Environment.NewLine);
                    Trace.WriteLine(Environment.NewLine);
                }
            }
            Trace.WriteLine("----------------------------------------------------------------------------------------------------");
        }

        // Prints the grid to console (template version)
        public void PrintGrid<T>(T[,] Grid, int padding = 20)
        {
            Trace.WriteLine("----------------------------------------------------------------------------------------------------");

            for (int Row = 0; Row <= (Grid.GetUpperBound(0)); Row++)
            {
                for (int Col = 0; Col <= (Grid.GetUpperBound(1)); Col++)
                {
                    Trace.Write(Grid[Row, Col].ToString().PadRight(padding, ' '));
                }
                Trace.WriteLine(Environment.NewLine);
                Trace.WriteLine(Environment.NewLine);
                Trace.WriteLine(Environment.NewLine);
                Trace.WriteLine(Environment.NewLine);
                Trace.WriteLine(Environment.NewLine);
            }
            Trace.WriteLine("----------------------------------------------------------------------------------------------------");
        }

        public static void FlipGridH<T>(T[,] Grid)
        {
            for (int Row = 0; Row <= (Grid.GetUpperBound(0)); Row++)
            {
                for (int Col = 0; Col <= (Grid.GetUpperBound(1) / 2); Col++)
                {
                    T TempHolder = Grid[Row, Col];
                    Grid[Row, Col] = Grid[Row, Grid.GetUpperBound(1) - Col];
                    Grid[Row, Grid.GetUpperBound(1) - Col] = TempHolder;
                }
            }
        }

        public static void TransposeGrid<T>(T[,] Grid)
        {
            T[,] TempGrid = Grid.Clone() as T[,];

            for (int Row = 0; Row <= (Grid.GetUpperBound(0)); Row++)
            {
                for (int Col = 0; Col <= (Grid.GetUpperBound(1)); Col++)
                {
                    Grid[Row, Col] = TempGrid[Col, Row];
                }
            }
        }

        public static void RotateGridCW<T>(T[,] Grid)
        {
            TransposeGrid(Grid);
            FlipGridH(Grid);
        }

        public static void PermuteTiers(int[,] Grid)
        {
            for (int Row = 0; Row <= (Grid.GetUpperBound(0)); Row++)
            {
                for (int Col = 0; Col <= (Grid.GetUpperBound(1)); Col++)
                {
                    Grid[Row, Col]++;
                    if (Grid[Row, Col] == 6)
                        Grid[Row, Col] = 1;
                }
            }
        }
        public bool CheckTierFit(int[,] TierGrid, List<Goal> MainList)
        {
            /*for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if (!MainList[i * 5 + j].PossibleTiers.Contains(TierGrid[i, j]))
                    {
                        return false;
                    }
                }
            }*/
            // I unrolled the loop for a small performance boost.
            if (!MainList[0].PossibleTiers.Contains(TierGrid[0, 0])) return false;  if (!MainList[1].PossibleTiers.Contains(TierGrid[0, 1])) return false;
            if (!MainList[2].PossibleTiers.Contains(TierGrid[0, 2])) return false;  if (!MainList[3].PossibleTiers.Contains(TierGrid[0, 3])) return false;
            if (!MainList[4].PossibleTiers.Contains(TierGrid[0, 4])) return false;  if (!MainList[5].PossibleTiers.Contains(TierGrid[1, 0])) return false;
            if (!MainList[6].PossibleTiers.Contains(TierGrid[1, 1])) return false;  if (!MainList[7].PossibleTiers.Contains(TierGrid[1, 2])) return false;
            if (!MainList[8].PossibleTiers.Contains(TierGrid[1, 3])) return false;  if (!MainList[9].PossibleTiers.Contains(TierGrid[1, 4])) return false;
            if (!MainList[10].PossibleTiers.Contains(TierGrid[2, 0])) return false; if (!MainList[11].PossibleTiers.Contains(TierGrid[2, 1])) return false;
            if (!MainList[12].PossibleTiers.Contains(TierGrid[2, 2])) return false; if (!MainList[13].PossibleTiers.Contains(TierGrid[2, 3])) return false;
            if (!MainList[14].PossibleTiers.Contains(TierGrid[2, 4])) return false; if (!MainList[15].PossibleTiers.Contains(TierGrid[3, 0])) return false;
            if (!MainList[16].PossibleTiers.Contains(TierGrid[3, 1])) return false; if (!MainList[17].PossibleTiers.Contains(TierGrid[3, 2])) return false;
            if (!MainList[18].PossibleTiers.Contains(TierGrid[3, 3])) return false; if (!MainList[19].PossibleTiers.Contains(TierGrid[3, 4])) return false;
            if (!MainList[20].PossibleTiers.Contains(TierGrid[4, 0])) return false; if (!MainList[21].PossibleTiers.Contains(TierGrid[4, 1])) return false;
            if (!MainList[22].PossibleTiers.Contains(TierGrid[4, 2])) return false; if (!MainList[23].PossibleTiers.Contains(TierGrid[4, 3])) return false;
            if (!MainList[24].PossibleTiers.Contains(TierGrid[4, 4])) return false;
            return true;
        }

        public void Shuffle<T>(IList<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /*
        // Checks a grid to see if ints are exclusive to their row/column/diagonal. This version of this function is currently unused as
        // more features needed to be added that were specific for GoalCategories and Goals.
        // It is also OUTDATED because there is a bug with EBs, if two categories share an EB they because exclusive to each other as well.
        public bool CheckGridInt(int[,] grid)
        {
            var rows = new HashSet<int>[5];
            var cols = new HashSet<int>[5];
            var diagonals = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };
            
            for (int i = 0; i < 5; i++)
            {
                rows[i] = new HashSet<int>();
                cols[i] = new HashSet<int>();
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    List<int> vals = new List<int>();

                    // if(selfexclusive) This version of the function doesn't have a nice way to check this

                    vals.Add(grid[i, j]);

                    if (ExclusivityBuddiesTable.ContainsKey(grid[i, j]))
                    {
                        foreach (var EBValue in ExclusivityBuddiesTable[grid[i, j]])
                        {
                            vals.Add(EBValue);
                        }
                    }

                    foreach (var val in vals)
                    {
                        if (rows[i].Contains(val) || cols[j].Contains(val) || (i == j && diagonals[0].Contains(val)) || (i + j == 4 && diagonals[1].Contains(val)))
                        {
                            return false;
                        }
                        rows[i].Add(val);
                        cols[j].Add(val);
                        if (i == j)
                        {
                            diagonals[0].Add(val);
                        }
                        if (i + j == 4)
                        {
                            diagonals[1].Add(val);
                        }
                    }
                }
            }

            return true;
        }
        */

        // Checks a grid to see if the GoalCategories are exclusive to their row/column/diagonal. This version is used during the initial
        // category generation. This version checks the entire board.
        public bool CheckGridCategory(GoalCategory[,] Grid)
        {
            var rows = new HashSet<int>[5];
            var cols = new HashSet<int>[5];
            var diagonals = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };
            var rowsEB = new HashSet<int>[5];
            var colsEB = new HashSet<int>[5];
            var diagonalsEB = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };

            for (int i = 0; i < 5; i++)
            {
                rows[i] = new HashSet<int>();
                cols[i] = new HashSet<int>();
                rowsEB[i] = new HashSet<int>();
                colsEB[i] = new HashSet<int>();
            }

            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if (Grid[row, col].Name == "Any")
                        continue;
                    
                    int currentCategoryID = Grid[row, col].ID;
                    bool currentIsSE = Grid[row, col].SelfExclusive;

                    // If the current category is self-exclusive, check if any relevant line contains the category.
                    if (currentIsSE)
                    {
                        if (rows[row].Contains(currentCategoryID) || cols[col].Contains(currentCategoryID) || (row == col && diagonals[0].Contains(currentCategoryID)) || (row + col == 4 && diagonals[1].Contains(currentCategoryID)))
                            return false;
                    }

                    // For current category, check if any relevant line has it as an exclusivity buddy.
                    if (rowsEB[row].Contains(currentCategoryID) || colsEB[col].Contains(currentCategoryID) || (row == col && diagonalsEB[0].Contains(currentCategoryID)) || (row + col == 4 && diagonalsEB[1].Contains(currentCategoryID)))
                        return false;

                    rows[row].Add(currentCategoryID);
                    cols[col].Add(currentCategoryID);
                    if (row == col)
                    {
                        diagonals[0].Add(currentCategoryID);
                    }
                    if (row + col == 4)
                    {
                        diagonals[1].Add(currentCategoryID);
                    }

                    foreach (int val in Grid[row, col].ExclusivityBuddiesIDs)
                    {
                        rowsEB[row].Add(val);
                        colsEB[col].Add(val);
                        if (row == col)
                        {
                            diagonalsEB[0].Add(val);
                        }
                        if (row + col == 4)
                        {
                            diagonalsEB[1].Add(val);
                        }
                    }
                }
            }

            return true;
        }

        // -Old version of above for testing-
        // Checks a grid to see if the GoalCategories are exclusive to their row/column/diagonal. This version is used during the initial
        // category generation. Some categories have exclusivity buddies and can be non self exclusive, this function is where those
        // variables are relevant.
        public bool CheckGridCategoryOld(GoalCategory[,] Grid)
        {
            var rows = new HashSet<int>[5];
            var cols = new HashSet<int>[5];
            var diagonals = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>() };

            for (int i = 0; i < 5; i++)
            {
                rows[i] = new HashSet<int>();
                cols[i] = new HashSet<int>();
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    List<int> vals = new List<int>();

                    if (Grid[i, j].SelfExclusive)
                        vals.Add(Grid[i, j].ID);

                    vals.AddAll(Grid[i, j].ExclusivityBuddiesIDs);

                    foreach (var val in vals)
                    {
                        if (rows[i].Contains(val) || cols[j].Contains(val) || (i == j && diagonals[0].Contains(val)) || (i + j == 4 && diagonals[1].Contains(val)))
                        {
                            return false;
                        }
                    }

                    foreach (var val in vals)
                    {
                        rows[i].Add(val);
                        cols[j].Add(val);
                        if (i == j)
                        {
                            diagonals[0].Add(val);
                        }
                        if (i + j == 4)
                        {
                            diagonals[1].Add(val);
                        }
                    }
                }
            }

            return true;
        }

        // Unused
        CheckGridHashSets GetMainListCGHS()
        {
            CheckGridHashSets result = new CheckGridHashSets();

            for (int i = 0; i < 25; i++)
            {
                int Size = 5;

                int row = i / Size;
                int col = i % Size;

                int currentCategoryID = MainList[i].GoalCategories[0].ID;
                string currentCategoryName = MainList[i].GoalCategories[0].Name;

                result.rows[row].Add(currentCategoryID);
                result.cols[col].Add(currentCategoryID);
                if (row == col)
                {
                    result.diagonals[0].Add(currentCategoryID);
                }
                if (row + col == 4)
                {
                    result.diagonals[1].Add(currentCategoryID);
                }

                foreach (int val in MainList[i].GoalCategories[0].ExclusivityBuddiesIDs)
                {
                    result.rowsEB[row].Add(val);
                    result.colsEB[col].Add(val);
                    if (row == col)
                    {
                        result.diagonalsEB[0].Add(val);
                    }
                    if (row + col == 4)
                    {
                        result.diagonalsEB[1].Add(val);
                    }
                }
            }

            return result;
        }

        CheckGridDictionaries GetMainListCGD()
        {
            CheckGridDictionaries result = new CheckGridDictionaries();

            for (int i = 0; i < 25; i++)
            {
                int Size = 5;

                int row = i / Size;
                int col = i % Size;

                int currentCategoryID = MainList[i].GoalCategories[0].ID;
                string currentCategoryName = MainList[i].GoalCategories[0].Name;

                result.rows.AddNew(row, currentCategoryID, col);
                result.cols.AddNew(col, currentCategoryID, row);

                if (row == col)
                {
                    result.diagonals.AddNew(0, currentCategoryID, row);
                }
                if (row + col == 4)
                {
                    result.diagonals.AddNew(1, currentCategoryID, row);
                }

                foreach (int val in MainList[i].GoalCategories[0].ExclusivityBuddiesIDs)
                {
                    result.rowsEB.AddNew(row, val, col);
                    result.colsEB.AddNew(col, val, row);
                    if (row == col)
                    {
                        result.diagonalsEB.AddNew(0, val, row);
                    }
                    if (row + col == 4)
                    {
                        result.diagonalsEB.AddNew(1, val, row);
                    }
                }
            }

            return result;
        }

        void CheckGridPossibleTiers(int cell, CheckGridDictionaries CGD)
        {
            GoalCategory CurrentFirstGoalCategory = MainList[cell].GoalCategories[0];

            //int MLIndex = row * 5 + col;

            //if (MLIndex == cell) // Skip the cell that we are checking
            //    continue;

            int row = cell / 5;
            int col = cell % 5;

            for (int tier = 1; tier <= 5; tier++)
            {
                if (!MainList[cell].PossibleTiers.Contains(tier))
                    continue;

                HashSet<int> allCurrentCategories = new HashSet<int>();
                HashSet<int> currentSECategories = new HashSet<int>();
                //HashSet<int> currentEBCategories = new HashSet<int>();
                int pureChallenge = CurrentFirstGoalCategory.IsChallengeOnly ? 1 : 0;

                HashSet<GoalCategory> CommonCategories = SortedGoalsCommonCategories[CurrentFirstGoalCategory.ID][tier - 1][pureChallenge][1];
                if(CommonCategories.Count < 1) // If the commoncategories limited is empty, use the unlimited one, just like with SortedGoals
                    CommonCategories = SortedGoalsCommonCategories[CurrentFirstGoalCategory.ID][tier - 1][pureChallenge][0];

                foreach (var category in CommonCategories)
                {
                    if (category == CurrentFirstGoalCategory)
                        continue;
                            
                    allCurrentCategories.Add(category.ID);

                    if (category.SelfExclusive)
                        currentSECategories.Add(category.ID);

                }

                bool removed = false;

                // For each self-exclusive category of the current goal, check if any relevant line contains that category.
                foreach (var val in currentSECategories)
                {

                    // If the current row has the self-exclusive category, and we aren't talking about the current cell.
                    if (CGD.rows[row].ContainsKey(val) && CGD.rows[row][val].Any(value => value != col))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        removed = true;
                        break; 
                    }

                    // If the current col has the self-exclusive category, and we aren't talking about the current cell.
                    if (CGD.cols[col].ContainsKey(val) && CGD.cols[col][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        removed = true;
                        break;
                    }

                    // If the first diagonal is relevant and it has the self-exclusive category, and we aren't talking about the current cell.
                    if ((row == col && CGD.diagonals[0].ContainsKey(val)) && CGD.diagonals[0][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        removed = true;
                        break;
                    }

                    // If the second diagonal is relevant and it has the self-exclusive category, and we aren't talking about the current cell.
                    if ( (row + col == 4 && CGD.diagonals[1].ContainsKey(val)) && CGD.diagonals[1][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        removed = true;
                        break;
                    }
                }

                if (removed)
                    continue;

                // For each and all current categories of the current goal, check if any relevant line has it as an exclusivity buddy.
                foreach (var val in allCurrentCategories)
                {
                    if (CGD.rowsEB[row].ContainsKey(val) && CGD.rowsEB[row][val].Any(value => value != col))
                    {
                        /* //debugging help
                        HashSet<int> tempSet = new HashSet<int>(CGD.rowsEB[row][val]);tempSet.Remove(col);
                        if (tempSet.Count > 1) foreach (var entry in tempSet) { tempSet.Remove(entry); if (tempSet.Count == 1) break; }
                        Goal tempGoal = MainList[ row * 5 + tempSet.Single()  ];
                        GoalCategory protestingCellCat = tempGoal.GoalCategories[0];
                        GoalCategory thisCellCat = MainList[cell].GoalCategories[0];
                        GoalCategory thisConflictCat = Categories[CategoryNamesByID[val]];
                        */

                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        break;
                    }
                    else if (CGD.colsEB[col].ContainsKey(val) && CGD.colsEB[col][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        break;
                    }
                    else if (row == col && CGD.diagonalsEB[0].ContainsKey(val) && CGD.diagonalsEB[0][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        break;
                    }
                    else if (row + col == 4 && CGD.diagonalsEB[1].ContainsKey(val) && CGD.diagonalsEB[1][val].Any(value => value != row))
                    {
                        MainList[cell].PossibleTiers.Remove(tier); // This may be too simple, we might want to check using SortedGoalsCommonCategories, i don't know yet.
                        break;
                    }
                }

                // When we find a match, which one do we remove the possible tier from? I guess the one that has the most possible
                // tiers...
                // ^ I wrote this before MainList[cell].PossibleTiers.Remove(tier); there may be a more optimal solution but that may be overkill
            }
        }
        
        // Old attempts at optimization, unused because they didn't help
        bool CanNumbersFitHmmm()
        {
            // Rows
            for (int i = 0; i < 5; i++)
                if (!CanNumbersFitInRow(new List<HashSet<int>> 
                    { MainList[i + 0].PossibleTiers, MainList[i + 1].PossibleTiers, MainList[i + 2].PossibleTiers, MainList[i + 3].PossibleTiers, MainList[i + 4].PossibleTiers }
                ))
                    return false;
            // Cols
            for (int i = 0; i < 5; i++)
                if (!CanNumbersFitInRow(new List<HashSet<int>>
                    { MainList[i].PossibleTiers, MainList[i + 5*1].PossibleTiers, MainList[i + 5*2].PossibleTiers, MainList[i + 5*3].PossibleTiers, MainList[i + 5*4].PossibleTiers }
                ))
                    return false;
            /*
            // Diagonal
            if (!CanNumbersFitInRow(new List<List<int>>{ MainList[0].PossibleTiers, MainList[6].PossibleTiers, MainList[12].PossibleTiers, MainList[18].PossibleTiers, MainList[24].PossibleTiers }))
                return false;

            // Other Diagonal
            if (!CanNumbersFitInRow(new List<List<int>> { MainList[4].PossibleTiers, MainList[8].PossibleTiers, MainList[12].PossibleTiers, MainList[16].PossibleTiers, MainList[20].PossibleTiers }))
                return false;
            */
            return true; // All numbers can fit in at least one slot
        }
        bool CanNumbersFitInRow(List<HashSet<int>> slots)
        {
            // Check if each number can fit in at least one slot
            for (int num = 1; num <= 5; num++)
            {
                bool canFitNumber = false;

                foreach (var slot in slots)
                {
                    if (slot.Contains(num))
                    {
                        canFitNumber = true;
                        break;
                    }
                }

                if (!canFitNumber)
                    return false; // Number cannot fit in any slot
            }

            return true; // All numbers can fit in at least one slot
        }
        public bool CanFitNumbers()
        {
            return CanFitNumbersRecursive(0);
        }
        private bool CanFitNumbersRecursive(int index)
        {
            int Size = 5;

            if (index == MainList.Count)
            {
                return true; // All cells are filled
            }

            int row = index / Size;
            int col = index % Size;

            foreach (var num in MainList[index].PossibleTiers)
            {
                bool isNumUsedInRow = false;
                bool isNumUsedInColumn = false;
                bool isNumUsedInDiagonal = false;
                bool isNumUsedInAntiDiagonal = false;

                for (int i = 0; i < Size; i++)
                {
                    if (MainList[row * Size + i].AmountPerTier == num)
                    {
                        isNumUsedInRow = true;
                        break;
                    }
                }

                for (int i = 0; i < Size; i++)
                {
                    if (MainList[i * Size + col].AmountPerTier == num)
                    {
                        isNumUsedInColumn = true;
                        break;
                    }
                }

                if (row == col)
                {
                    for (int i = 0; i < Size; i++)
                    {
                        if (MainList[i * Size + i].AmountPerTier == num)
                        {
                            isNumUsedInDiagonal = true;
                            break;
                        }
                    }
                }

                if (row + col == Size - 1)
                {
                    for (int i = 0; i < Size; i++)
                    {
                        if (MainList[i * Size + (Size - i - 1)].AmountPerTier == num)
                        {
                            isNumUsedInAntiDiagonal = true;
                            break;
                        }
                    }
                }

                // If the number is not used in any of the ways, assign it to the current cell and continue.
                if (!isNumUsedInRow && !isNumUsedInColumn && !isNumUsedInDiagonal && !isNumUsedInAntiDiagonal)
                {
                    MainList[index].AmountPerTier = num;

                    // Recursively try to fill the next cell.
                    if (CanFitNumbersRecursive(index + 1))
                    {
                        return true;
                    }

                    // If it doesn't lead to a valid solution, backtrack and try the next number.
                    MainList[index].AmountPerTier = -1;
                }
            }

            // If no number can be placed in this cell, return false.
            return false;
        }

        //public void ShuffleCategoryExclusive<T>(this IList<T> list)
        //{
        //    int n = list.Count;
        //    while (n > 1)
        //    {
        //        n--;
        //        int k = rand.Next() % n + 1;
        //        T value = list[k];
        //        list[k] = list[n];
        //        list[n] = value;
        //    }
        //}

        //public void ModifyJSONGoalListFormat()
        //{
        //    JArray BaseGoalsJArray = JArray.Parse(File.ReadAllText(Globals.BaseFilePathMod + @"Data\GoalList.json"));

        //    foreach (var jObj in BaseGoalsJArray)
        //    {
        //        //jObj.

        //        //BaseGoalsJArray.Add

        //        //BaseGoalsJArray.Add(-19, JObject["AmountMin"]);
        //        //BaseGoalsJArray.Add(1-1, JObject["AmountMax"]);
        //        //BaseGoalsJArray.Add(11, JObject["AmountStep"]);
        //        //BaseGoalsJArray.Add(12, JObject["AmountTierAdd"]);
        //        //BaseGoalsJArray.Add(13, JObject["AmountTierAddExtra"]);
        //    }

        //    using (StreamWriter file = File.CreateText(Globals.BaseFilePathMod + @"Data\GoalList2.json"))
        //    using (JsonTextWriter writer = new JsonTextWriter(file))
        //    {
        //        BaseGoalsJArray.WriteTo(writer);
        //    }
        //}

        public void CategoryHasGoals(List<List<List<Goal>>> GoalList, ref bool HasNonChallengeOnlyGoals, ref bool HasChallengeOnlyGoals)
        {
            HasNonChallengeOnlyGoals = false;
            HasChallengeOnlyGoals = false;

            foreach (var l in GoalList[0])
            {
                if (l.Count > 0)
                {
                    HasNonChallengeOnlyGoals = true;
                    break;
                }
            }
            foreach (var l in GoalList[1])
            {
                if (l.Count > 0)
                {
                    HasChallengeOnlyGoals = true;
                    break;
                }
            }
        }

        public static List<double> CategorySoftmax(List<GoalCategory> categories, double temperature = 4.0)
        {
            // Get the valid chances (where CanReplaceAny is true)
            List<double> validChances = categories.Where(c => c.CanReplaceAny).Select(c => (double)c.Chance / temperature).ToList();

            // Calculate the sum of exp(chance) for all valid chances
            double sum = validChances.Sum(chance => Math.Exp(chance));

            List<double> results = new List<double>();

            foreach (var category in categories)
            {
                if (category.CanReplaceAny)
                {
                    // Calculate the softmax value
                    double softmaxValue = Math.Exp((double)category.Chance / temperature) / sum;
                    results.Add(softmaxValue);
                }
                else
                {
                    // If CanReplaceAny is false, add 0 to the results
                    results.Add(0);
                }
            }

            return results;
        }

        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        //		Specific Goal Generators
        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        public void CalculateGoalAmount(Goal InputGoal, int TargetTier)
        {
            List<int> ValidAmounts = new List<int>();

            int TotalSteps = ((InputGoal.AmountMax - InputGoal.AmountMin) / InputGoal.AmountStep) + 1;
            for (int i = 0; i < TotalSteps; i++)
            {
                int CurrentAmountToTest = InputGoal.AmountMin + (i * InputGoal.AmountStep);
                int TierAdd = (CurrentAmountToTest - InputGoal.AmountMin) / InputGoal.AmountPerTier;

                if (InputGoal.Region != -1)
                    TierAdd += U.RegionDistance(StartingArea, InputGoal.Region, LoSExitWaygate);

                if (InputGoal.AmountPerTier == -1)
                    TierAdd = 0;
                if (InputGoal.AmountExtraTierBreakpoint != -1)
                    if (CurrentAmountToTest >= InputGoal.AmountExtraTierBreakpoint)
                        TierAdd++;

                if (InputGoal.TierMin + TierAdd == TargetTier)
                    ValidAmounts.Add(CurrentAmountToTest);
                else if (InputGoal.TierMin + TierAdd > TargetTier)
                    break;
            }            
            InputGoal.Amount = ValidAmounts[BingoRand.Next() % ValidAmounts.Count];
            InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|X|", InputGoal.Amount.ToString());
        }

        public void CalculateGoalAmountAndRegion(Goal InputGoal, int TargetTier)
        {
            Dictionary<int, List<int>> ValidRegionsAndAmounts = new Dictionary<int, List<int>>();

            int TotalSteps = ((InputGoal.AmountMax - InputGoal.AmountMin) / InputGoal.AmountStep) + 1;

            if (InputGoal.GoalDesc == "|X| Catacombs in |Region|" && StartingArea == 0 && TargetTier == 4 && EnabledRegions[1])
            {
                int delLater = 0;
            }

            for (int Region = 0; Region < Globals.TotalRegionAmount; Region++)
            {
                if (EnabledRegions[Region] == false)
                    continue; // Skip if the region is disabled

                ValidRegionsAndAmounts[Region] = new List<int>();
                int RegionTierAdd = U.RegionDistance(StartingArea, Region, LoSExitWaygate);

                for (int Step = 0; Step < TotalSteps; Step++)
                {
                    int CurrentAmountToTest = InputGoal.AmountMin + (Step * InputGoal.AmountStep);
                    int TierAdd = (CurrentAmountToTest - InputGoal.AmountMin) / InputGoal.AmountPerTier;
                    if (InputGoal.AmountExtraTierBreakpoint != -1)
                        if (CurrentAmountToTest >= InputGoal.AmountExtraTierBreakpoint)
                            TierAdd++;

                    if (InputGoal.TierMin + TierAdd + RegionTierAdd == TargetTier)
                        ValidRegionsAndAmounts[Region].Add(CurrentAmountToTest);
                }
            }
            foreach (var Region in ValidRegionsAndAmounts)
                if (Region.Value.Count <= 0)
                    ValidRegionsAndAmounts.Remove(Region.Key);

            if(InputGoal.GoalDesc == "|X| Catacombs in |Region|" && ValidRegionsAndAmounts.ContainsKey(0))
            {
                ValidRegionsAndAmounts.Remove(0);
                if(ValidRegionsAndAmounts.Count == 0)
                    ValidRegionsAndAmounts[StartingArea > 2 ? 4 : 1] = new List<int>() { TargetTier == 5 ? 3 : 2 };
            }

            int RandomRegionIndex = BingoRand.Next() % ValidRegionsAndAmounts.Count;
            int RandomAmountIndex = BingoRand.Next() % ValidRegionsAndAmounts.ElementAt(RandomRegionIndex).Value.Count;
            InputGoal.Amount = ValidRegionsAndAmounts.ElementAt(RandomRegionIndex).Value[RandomAmountIndex];
            InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|X|", ValidRegionsAndAmounts.ElementAt(RandomRegionIndex).Value[RandomAmountIndex].ToString());
            InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Region|", Globals.AreaNames[ValidRegionsAndAmounts.ElementAt(RandomRegionIndex).Key]);
        }

        public void CalculateRegion(Goal InputGoal, int TargetTier)
        {
            List<int> ValidRegions = new List<int>();
            for (int Region = 0; Region < Globals.TotalRegionAmount; Region++)
            {
                if (EnabledRegions[Region] == false)
                    continue; // Skip if the region is disabled

                int DistanceTierAdd = U.RegionDistance(StartingArea, Region, LoSExitWaygate);

                if (InputGoal.TierMin + DistanceTierAdd == TargetTier)
                    ValidRegions.Add(Region);
            }
            int RandomRegion = BingoRand.Next() % ValidRegions.Count;
            InputGoal.Region = ValidRegions[RandomRegion];
            InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Region|", Globals.AreaNames[InputGoal.Region]);
        }

        public int RemapToRange(int Value, int Low1, int High1, int Low2, int High2)
        {
            return Low2 + (Value - Low1) * (High2 - Low2) / (High1 - Low1);
        }

        public void GenerateAttributeGoal(Goal InputGoal, int TargetTier)
        {
            List<string> EligibleAttributes = null;
            string RandomAttribute1 = null;
            string RandomAttribute2 = null;

            switch (InputGoal.GoalDesc)
            {
                case "60 |Attribute1|":
                    EligibleAttributes = new List<string>() { "Strength", "Dexterity" };
                    RandomAttribute1 = EligibleAttributes[BingoRand.Next() % EligibleAttributes.Count];
                    InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Attribute1|", RandomAttribute1);
                    break;
                case "50 |Attribute1|":
                    EligibleAttributes = new List<string>() { "Mind", "Endurance", "Intelligence", "Faith", "Arcane" };
                    RandomAttribute1 = EligibleAttributes[BingoRand.Next() % EligibleAttributes.Count];
                    InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Attribute1|", RandomAttribute1);
                    break;
                case "20 |Attribute1| and |Attribute2|":
                case "30 |Attribute1| and |Attribute2|":
                case "40 |Attribute1| and |Attribute2|":
                case "50 |Attribute1| and |Attribute2|":
                    EligibleAttributes = new List<string>() { "Mind", "Endurance", "Strength", "Dexterity", "Intelligence", "Faith", "Arcane" };
                    RandomAttribute1 = EligibleAttributes[BingoRand.Next() % EligibleAttributes.Count];
                    InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Attribute1|", RandomAttribute1);

                    EligibleAttributes.Remove(RandomAttribute1);
                    RandomAttribute2 = EligibleAttributes[BingoRand.Next() % EligibleAttributes.Count];
                    InputGoal.GoalDesc = InputGoal.GoalDesc.Replace("|Attribute2|", RandomAttribute2);
                    break;
                default:
                    break;
            }
        }

        public void GenerateChallengeGoal(Goal InputGoal, int TargetTier)
        {
            List<ChallengeModifierFinal> ValidChallenges = new List<ChallengeModifierFinal>();
            int TotalTier = -1;

            foreach (var ChallengeModifier in FullChallengeModifierList)
            {
                // Skip weapon collection goals if they're not allowed
                if (!Globals.WeaponCollectionGoalsAllowed && ChallengeModifier.IsWeaponCollection == "Main")
                    continue;
                if (!Globals.SidearmCollectionGoalsAllowed && ChallengeModifier.IsWeaponCollection == "Sidearm")
                    continue;

                if (ChallengeModifier.ChallengeTier > InputGoal.ChallengeRange)
                    continue;

                // Exclude non-EnemyChallengeEligible challenges for EnemyGroupChallenges
                if (!ChallengeModifier.EnemyChallengeEligible && InputGoal.GoalCategories.Contains(Categories["EnemyGroupChallenges"]))
                    continue;

                TotalTier = InputGoal.TierChallengeBase + ChallengeModifier.BaseTierAdd;

                if (Globals.ModifiersEnabled)
                    foreach (var ModifierTierChange in ChallengeModifier.ModifierTierChanges)
                        for (int ModifierIndex = 0; ModifierIndex < Globals.CurrentModifierCount; ModifierIndex++)
                        {
                            string ModifierName = Globals.CurrentModifierList[ModifierIndex].Name;
                            if (ModifierName == ModifierTierChange.Key)
                                TotalTier += ModifierTierChange.Value;
                        }

                bool BossPoolIsExcluded = false;
                foreach (var ExcludedBossPool in ChallengeModifier.ExcludedBossPools)
                    if (InputGoal.GoalCategories.Contains(Categories[ExcludedBossPool]))
                    {
                        BossPoolIsExcluded = true;
                        break;
                    }

                if (!BossPoolIsExcluded && !ChallengeModifier.ForbiddenGoals.Contains(InputGoal.GoalDesc))
                {
                    if (TotalTier == TargetTier)
                        ValidChallenges.Add(new ChallengeModifierFinal(ChallengeModifier, false));
                    else if (ChallengeModifier.SoloEligible == "Yes" && !InputGoal.GoalCategories.Contains(Categories["Evergaols"]) && TotalTier + 1 == TargetTier)
                        ValidChallenges.Add(new ChallengeModifierFinal(ChallengeModifier, true));
                    else if (NoTierLogic)
                        ValidChallenges.Add(new ChallengeModifierFinal(ChallengeModifier, false));
                }
            }

            // Set up goal list
            Shuffle(ValidChallenges);

            //Trace.WriteLine(Environment.NewLine + "Before:");
            //foreach (var ValidChallenge in ValidChallenges)
            //    Trace.WriteLine(ValidChallenge.ChallengeModifier.ChallengeTier);

            // BonusProbability
            // Goals can have a bonus probability, it is a chance for the goal to be moved to the top of the list of possible goals.
            // If the probability is negative it gets moved to the bottom of the list instead, but the chance of it happening is the absolute value.
            if (ValidChallenges.Count > 1)
            {
                for (int i = 0; i < ValidChallenges.Count; i++)
                {
                    ChallengeModifierFinal ChallengeModifierFinal = ValidChallenges[i];
            
                    int BonusProbability = ChallengeModifierFinal.ChallengeModifier.BonusProbability;
            
                    if (NoTierLogic)
                        BonusProbability = BonusProbability / 9;
            
                    if (BonusProbability != 0)
                    {
                        bool Positive = BonusProbability > 0;
                        int Prob = Math.Abs(BonusProbability);
                        
                        if ((BingoRand.Next() % 100) + 1 <= Prob) // Whether the random chance with the probability succeeds
                        {
                            ValidChallenges.Remove(ChallengeModifierFinal);
                            ValidChallenges.Insert(Positive ? 0 : ValidChallenges.Count, ChallengeModifierFinal);
                        }
                    }
                }
            }

            // Move tier 3 and 2 goals to top because they have low priority otherwise
            // List<ChallengeModifierFinal> TempCMFList1 = new List<ChallengeModifierFinal>();
            // List<ChallengeModifierFinal> TempCMFList2 = new List<ChallengeModifierFinal>();
            // List<ChallengeModifierFinal> TempCMFList3 = new List<ChallengeModifierFinal>();
            // foreach (var ValidChallenge in ValidChallenges.ToList())
            //     if (ValidChallenge.ChallengeModifier.ChallengeTier == 1)
            //         TempCMFList1.Add(ValidChallenge);
            //     else if (ValidChallenge.ChallengeModifier.ChallengeTier == 2)
            //         TempCMFList2.Add(ValidChallenge);
            //     else
            //         TempCMFList3.Add(ValidChallenge);
            // 
            // ValidChallenges.Clear();
            // ValidChallenges.AddRange(TempCMFList3);
            // ValidChallenges.AddRange(TempCMFList2);
            // ValidChallenges.AddRange(TempCMFList1);

            //Trace.WriteLine(Environment.NewLine + "After:");
            //foreach (var ValidChallenge in ValidChallenges)
            //    Trace.WriteLine(ValidChallenge.ChallengeModifier.ChallengeTier);

            // Goal selection and selection for solo
            bool CanBeSolo      = ValidChallenges.Find(x => x.IsSolo == true) != null;
            bool CanBeNotSolo   = !(ValidChallenges.Find(x => x.IsSolo == true) != null);

            // Exclude solo for EnemyGroupChallenges because it's messy and they're already wordy
            if (InputGoal.GoalCategories.Contains(Categories["EnemyGroupChallenges"]))
                CanBeSolo = false;

            bool ShouldBeSolo = ((BingoRand.Next() % 1000 < 250) && CanBeSolo);
            if (!CanBeNotSolo)
                ShouldBeSolo = true;

            //int RandomValidChallengeIndex = BingoRand.Next() % ValidChallenges.Count;
            //
            //while (ValidChallenges[RandomValidChallengeIndex].IsSolo != ShouldBeSolo)
            //    RandomValidChallengeIndex = BingoRand.Next() % ValidChallenges.Count;
            //
            //ChallengeModifier SelectedChallengeModifier = ValidChallenges[RandomValidChallengeIndex].ChallengeModifier;

            // Loop through list of valid challenges and select the first eligible
            ChallengeModifierFinal SelectedChallengeModifier = null;

            foreach (var ValidChallenge in ValidChallenges)
                if ((ShouldBeSolo && ValidChallenge.IsSolo) || (!ShouldBeSolo && !ValidChallenge.IsSolo))
                {
                    SelectedChallengeModifier = ValidChallenge;
                    break;
                }

            bool IsSolo = SelectedChallengeModifier.IsSolo;

            string FinalGoalDesc = InputGoal.GoalDesc;
            if (SelectedChallengeModifier.ChallengeModifier.IsFinishGoal)
            {
                if (FinalGoalDesc.Contains("Any"))
                    FinalGoalDesc = char.ToLower(FinalGoalDesc.First()) + FinalGoalDesc.Substring(1);
                FinalGoalDesc = FinalGoalDesc.Insert(0, "Finish ");
            }

            FinalGoalDesc += SelectedChallengeModifier.ChallengeModifier.ModifierDesc;
            if (IsSolo)
                FinalGoalDesc += " (solo)";
            if (FinalGoalDesc.Contains(" (solo)") && (InputGoal.GoalCategories.Contains(Categories["Evergaols"]) || Globals.IsModifierActive("ForbiddenSummoning") || !Globals.SpiritAshesEnabled))
                FinalGoalDesc = FinalGoalDesc.Remove(FinalGoalDesc.IndexOf(" (solo)"));

            foreach (var HD in SelectedChallengeModifier.ChallengeModifier.HintDescs)
                InputGoal.HintDescs.Add(HD);

            // EnemyGroupChallenges cleanup
            if (InputGoal.GoalCategories.Contains(Categories["EnemyGroupChallenges"]))
            {
                // Exclude solo for EnemyGroupChallenges because it's messy and they're already wordy
                if (FinalGoalDesc.Contains(" (solo)"))
                    FinalGoalDesc = FinalGoalDesc.Replace(" (solo)", "");

                // Also move (no reset) to the end of it
                if (FinalGoalDesc.Contains(" (no reset)"))
                {
                    FinalGoalDesc = FinalGoalDesc.Replace(" (no reset)", "");
                    FinalGoalDesc += " (no reset)";
                }
            }

            InputGoal.ChallengeTier = SelectedChallengeModifier.ChallengeModifier.ChallengeTier;
            InputGoal.GoalDesc = FinalGoalDesc;
            //string toReplace = InputGoal.BaseGoalDesc.Replace("Any", "any");
            //InputGoal.ChallengeIdentifier = FinalGoalDesc.Replace(toReplace, "");
            InputGoal.ChallengeIdentifier = 
                (SelectedChallengeModifier.ChallengeModifier.IsFinishGoal ? "Finish X" : "") +
                SelectedChallengeModifier.ChallengeModifier.ModifierDesc +
                (IsSolo ? " (solo)" : "");

            // Remove the current challenge goal from the list so it can't be selected again
            // todo: make it so challenge goals can't be removed if it's the only challenge goal left of its tier (irrelevant right now since all are same tier)
            //FullChallengeModifierList.Remove(SelectedChallengeModifier.ChallengeModifier);
        }

        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
        //		Misc.
        // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬

        public static void AddNewGoalListValue()
        {
            Dictionary<string, float> GoalDistributions = new Dictionary<string, float>()
            {
                ["Any boss"]                                                                  = 65.741f,
                ["Any final Legacy Dungeon boss"]                                             = 49.031f,
                ["|X| Underworld bosses"]                                                     = 45.053f,
                ["Restore any Great Rune"]                                                    = 40.346f,
                ["Loot |X| Spiritgraves ([Larval Tears])"]                                    = 40.086f,
                ["1 Land of Shadow Legacy Dungeon"]                                           = 36.148f,
                ["Any 2 Legacy Dungeons"]                                                     = 34.809f,
                ["Clear the rot pool of enemies (Miquella's Haligtree)"]                      = 28.123f,
                ["Any Hero's Grave boss"]                                                     = 27.883f,
                ["Any Legacy Dungeon"]                                                        = 26.144f,
                ["50 |Attribute1| and |Attribute2|"]                                          = 23.016f,
                ["Defeat 10 enemies in any Hero's Grave (no rest)"]                           = 22.297f,
                ["Any underworld boss"]                                                       = 22.007f,
                ["Any Cave/Tunnel boss"]                                                      = 21.537f,
                ["Have |X| Runes"]                                                            = 20.218f,
                ["Any Minor Dungeon boss"]                                                    = 20.198f,
                ["Rune level |X|"]                                                            = 19.638f,
                ["[Romina, Saint of the Bud]"]                                                = 19.478f,
                ["Any night boss"]                                                            = 18.769f,
                ["Any Catacomb boss"]                                                         = 18.609f,
                ["Any overworld boss"]                                                        = 18.599f,
                ["Touch 4 church ruin Graces"]                                                = 18.349f,
                ["Exhaust Coastal Merchant's stock"]                                          = 18.349f,
                ["|X| Minor Dungeons"]                                                        = 17.819f,
                ["Memory of Grace with |X| Runes"]                                            = 16.790f,
                ["40 |Attribute1| and |Attribute2|"]                                          = 16.450f,
                ["[Putrescent Knight]"]                                                       = 15.880f,
                ["|X| Caves/Tunnels/Gaols in |Region|"]                                       = 15.731f,
                ["Any Evergaol boss"]                                                         = 15.631f,
                ["[Godskin Apostle] (Caelid Divine Tower)"]                                   = 14.901f,
                ["[Midra, Lord of Frenzied Flame]"]                                           = 14.841f,
                ["30 Mind"]                                                                   = 14.621f,
                ["60 of any attribute"]                                                       = 14.431f,
                ["50 Vigor"]                                                                  = 14.281f,
                ["[Rellana, Twin Moon Knight]"]                                               = 14.251f,
                ["2 Gaol Dungeons"]                                                           = 14.072f,
                ["Loot |X| chests in Legacy Dungeons"]                                        = 14.022f,
                ["Hold |X| Stonesword Keys"]                                                  = 13.532f,
                ["|X| Minor Dungeons in |Region|"]                                            = 13.072f,
                ["|X| Catacombs"]                                                             = 12.982f,
                ["Have a >+20 standard weapon"]                                               = 12.982f,
                ["Have a >+8 unique weapon"]                                                  = 12.972f,
                ["Touch the Church Ruins Grace (Abyssal Woods)"]                              = 12.932f,
                ["Have a >+7 unique weapon and a >+17 standard weapon"]                       = 12.832f,
                ["[Cave of the Forlorn]"]                                                     = 12.812f,
                ["Defeat |X| NPC invaders"]                                                   = 12.592f,
                ["Touch Divine Bridge Grace"]                                                 = 12.473f,
                ["1 Mountaintops Legacy Dungeon"]                                             = 11.943f,
                ["|X| Caves/Tunnels/Gaols"]                                                   = 11.913f,
                ["Clear out Fort Gael"]                                                       = 11.873f,
                ["[Leyndell Catacombs] (Subterranean Shunning-Grounds)"]                      = 11.563f,
                ["|X| Evergaols"]                                                             = 11.413f,
                ["Restore Malenia's Great Rune"]                                              = 11.033f,
                ["|X| Overworld bosses (no Torrent)"]                                         = 10.893f,
                ["[Redmane Castle]"]                                                          = 10.454f,
                ["|X| Overworld bosses"]                                                      = 10.454f,
                ["|X| Night bosses (no Torrent)"]                                             = 10.094f,
                ["Defeat a Runebear"]                                                         = 10.064f,
                ["25 of all attributes (no buffs or rebirth)"]                                = 10.054f,
                ["|X| Night bosses in |Region| (no Torrent)"]                                 = 9.874f, 
                ["Defeat a Crystalian"]                                                       = 9.824f, 
                ["Give 2 Deathroots to Gurranq at once"]                                      = 9.724f, 
                ["Visit Ranni (Moonlight Altar)"]                                             = 9.634f, 
                ["Defeat 3 bosses without resting"]                                           = 9.634f, 
                ["[Tree Sentinel Duo]"]                                                       = 9.564f, 
                ["2 Ruined Forges"]                                                           = 9.534f, 
                ["|X| Night bosses"]                                                          = 9.374f, 
                ["[Fire Giant]"]                                                              = 9.334f, 
                ["[Placidusax] in the Heart of the Storm"]                                    = 9.264f, 
                ["20 of all attributes (no buffs or rebirth)"]                                = 9.105f, 
                ["Have |X| merchant Bell Bearing shops at Twin Maiden Husks"]                 = 8.995f, 
                ["Defeat the Great-Jar's champions"]                                          = 8.995f, 
                ["[Miquella's Haligtree]"]                                                    = 8.975f, 
                ["[Hidden Path to the Haligtree]"]                                            = 8.595f, 
                ["[Demi-Human Queen Maggie]"]                                                 = 8.505f, 
                ["1 Cave/Tunnel and 1 Catacomb in |Region|"]                                  = 8.475f, 
                ["Restore Radahn's Great Rune"]                                               = 8.315f, 
                ["[Impaler's Catacombs]"]                                                     = 8.265f, 
                ["All three Altus Hero's Graves"]                                             = 8.075f, 
                ["[Borealis, the Freezing Fog]"]                                              = 7.985f, 
                ["20 |Attribute1| and |Attribute2|"]                                          = 7.955f, 
                ["[Stormveil Castle]"]                                                        = 7.945f, 
                ["Get a headshot with any bow"]                                               = 7.835f, 
                ["[Volcano Manor]"]                                                           = 7.825f, 
                ["Exhaust Twin Maiden Husks' stock (main shop only)"]                         = 7.815f, 
                ["[Bayle the Dread]"]                                                         = 7.815f, 
                ["Any ruin boss"]                                                             = 7.735f, 
                ["Loot 3 Giant Stone Coffins (Cerulean Coast)"]                               = 7.725f, 
                ["Loot |X| Lands Between ruin chests"]                                        = 7.635f, 
                ["[Tombsward Cave]"]                                                          = 7.615f, 
                ["Any Minor Erdtree boss"]                                                    = 7.605f, 
                ["Loot |X| ruin main chests (includes Land of Shadow)"]                       = 7.575f, 
                ["70 of any attribute"]                                                       = 7.446f, 
                ["[Leyndell]"]                                                                = 7.376f, 
                ["[Full-Grown Fallingstar Beast] (Mt. Gelmir)"]                               = 7.366f, 
                ["Clear out Fort Gael and Fort Faroth"]                                       = 7.356f, 
                ["|X| Overworld bosses in |Region| (no Torrent)"]                             = 7.286f, 
                ["Restore Godrick's Great Rune"]                                              = 6.896f, 
                ["Sellia Evergaol"]                                                           = 6.876f, 
                ["Defeat [Mad Tongue Alberich]"]                                              = 6.676f, 
                ["[Demi-Human Queen Marigga]"]                                                = 6.656f, 
                ["[Magma Wyrm] (Mt. Gelmir)"]                                                 = 6.576f, 
                ["|X| Overworld bosses in |Region|"]                                          = 6.556f, 
                ["Clear out Fort Faroth"]                                                     = 6.516f, 
                ["[Abductor Virgins] (Volcano Manor)"]                                        = 6.506f, 
                ["Golden Lineage Evergaol"]                                                   = 6.436f, 
                ["[Rugalea the Great Red Bear]"]                                              = 6.406f, 
                ["[Fringefolk Hero's Grave]"]                                                 = 6.366f, 
                ["Loot |X| Land of Shadow ruin chests"]                                       = 6.366f, 
                ["[Coastal Cave]"]                                                            = 6.346f, 
                ["Any Legacy Dungeon boss"]                                                   = 6.336f, 
                ["Clear out Fort Laiedd"]                                                     = 6.326f, 
                ["[Black Knight Garrew]"]                                                     = 6.316f, 
                ["Defeat an NPC invader"]                                                     = 6.276f, 
                ["1 Altus Legacy Dungeon"]                                                    = 6.246f, 
                ["Clear out Fort Haight"]                                                     = 6.156f, 
                ["|X| Night bosses in |Region|"]                                              = 6.096f, 
                ["3 Bosses in Dragon's Pit Terminus/Jagged Peak"]                             = 6.086f, 
                ["Hold |X| Sacred Tears"]                                                     = 6.036f, 
                ["Defeat 10 enemies in Yelough Anix Ruins (no rest)"]                         = 5.966f, 
                ["Touch a Grace in each major Underworld region"]                             = 5.956f, 
                ["Royal Grave Evergaol"]                                                      = 5.886f, 
                ["Open the door near Patches spot (Volcano Manor)"]                           = 5.817f, 
                ["[Wyndham Catacombs]"]                                                       = 5.727f, 
                ["Have a +25 standard weapon"]                                                = 5.687f, 
                ["Loot a caravan chest"]                                                      = 5.677f, 
                ["Inflict Frostbite on the same boss 2 times"]                                = 5.677f, 
                ["Inflict Madness on yourself while in a boss fight"]                         = 5.677f, 
                ["[Giant-Conquering Hero's Grave]"]                                           = 5.657f, 
                ["Find Patches' Cave (Murkwater Cave)"]                                       = 5.637f, 
                ["[Divine Beast Dancing Lion] (Rauh Ruins)"]                                  = 5.617f, 
                ["1 Gaol Dungeon and 1 Ruined Forge"]                                         = 5.607f, 
                ["[Auriza Side Tomb]"]                                                        = 5.597f, 
                ["Have a +10 unique weapon"]                                                  = 5.557f, 
                ["[War-Dead Catacombs] (Redmane Castle)"]                                     = 5.547f, 
                ["Exhaust Academy Merchant's stock"]                                          = 5.477f, 
                ["[Jagged Peak Drake]"]                                                       = 5.467f, 
                ["Give a Deathroot to Gurranq"]                                               = 5.457f, 
                ["Light all 3 braziers in Sellia"]                                            = 5.447f, 
                ["[Gelmir Hero's Grave]"]                                                     = 5.437f, 
                ["Apply 3 different status effects on a boss"]                                = 5.417f, 
                ["Exhaust Imprisoned Merchant's stock"]                                       = 5.397f, 
                ["Exhaust Siofra River Merchant's stock"]                                     = 5.337f, 
                ["[Great Wyrm Theodorix]"]                                                    = 5.327f, 
                ["Any Hero's Grave"]                                                          = 5.307f, 
                ["[Auriza Hero's Grave]"]                                                     = 5.307f, 
                ["[Dual Jagged Peak Drake]"]                                                  = 5.217f, 
                ["[Grafted Scion]"]                                                           = 5.177f, 
                ["Exhaust Ainsel River Merchant's stock"]                                     = 5.117f, 
                ["Inflict Sleep on a boss"]                                                   = 5.117f, 
                ["[Scadutree Avatar]"]                                                        = 5.077f, 
                ["Defeat 2 bosses without resting"]                                           = 5.067f, 
                ["Defeat 15 enemies in Nokstella (no rest)"]                                  = 5.057f, 
                ["|X| Total Flask charges"]                                                   = 5.057f, 
                ["[Black Knight Edredd]"]                                                     = 5.057f, 
                ["[Sainted Hero's Grave]"]                                                    = 5.037f, 
                ["[Crumbling Farum Azula]"]                                                   = 5.007f, 
                ["[Commander O'Neil]"]                                                        = 4.987f, 
                ["[Black Blade Kindred] (Caelid)"]                                            = 4.967f, 
                ["[Ancient Dragon Senessax]"]                                                 = 4.947f, 
                ["[Ghostflame Dragon] (Scadu Altus)"]                                         = 4.917f, 
                ["Any [Ghostflame Dragon]"]                                                   = 4.897f, 
                ["Restore Morgott's and Mohg's Great Runes"]                                  = 4.887f, 
                ["[Royal Knight Loretta]"]                                                    = 4.877f, 
                ["[Mohg, the Omen]"]                                                          = 4.867f, 
                ["Have |X| Sacred Seals"]                                                     = 4.867f, 
                ["[Elemer of the Briar]"]                                                     = 4.847f, 
                ["Weeping Evergaol"]                                                          = 4.847f, 
                ["Clear out Guardians' Garrison"]                                             = 4.837f, 
                ["Defeat 15 enemies in Castle Morne (no rest)"]                               = 4.837f, 
                ["[Flying Dragon Greyll] (Dragonbarrow)"]                                     = 4.837f, 
                ["[Ghostflame Dragon] (Cerulean Coast)"]                                      = 4.807f, 
                ["Lord Contender's Evergaol"]                                                 = 4.797f, 
                ["[Leonine Misbegotten]"]                                                     = 4.767f, 
                ["[Omenkiller] (Village of the Albinaurics)"]                                 = 4.757f, 
                ["[Glintstone Dragon Smarag]"]                                                = 4.747f, 
                ["[Tombsward Catacombs]"]                                                     = 4.727f, 
                ["[Academy of Raya Lucaria]"]                                                 = 4.717f, 
                ["Defeat 10 enemies in Lake of Rot (no rest)"]                                = 4.707f, 
                ["Defeat 15 enemies in [Volcano Manor] (no rest)"]                            = 4.667f, 
                ["Loot 5 chests in Nokstella"]                                                = 4.657f, 
                ["Have |X| Glintstone Staves"]                                                = 4.617f, 
                ["[Commander Niall]"]                                                         = 4.607f, 
                ["Defeat 15 enemies in [Miquella's Haligtree] (no rest)"]                     = 4.557f, 
                ["[Tibia Mariner] (Limgrave)"]                                                = 4.547f, 
                ["[Ghostflame Dragon] (Gravesite Plain)"]                                     = 4.517f, 
                ["[Crucible Knight Siluria]"]                                                 = 4.497f, 
                ["[Consecrated Snowfield Catacombs]"]                                         = 4.487f, 
                ["2 Catacombs in |Region|"]                                                   = 4.477f, 
                ["Kick an enemy off a ledge to their death"]                                  = 4.467f, 
                ["Defeat a Crucible Knight"]                                                  = 4.417f, 
                ["Restore Rykard's Great Rune"]                                               = 4.387f, 
                ["Defeat a Godskin enemy"]                                                    = 4.387f, 
                ["Any Gaol Dungeon"]                                                          = 4.387f, 
                ["Hold |X| Golden Seeds"]                                                     = 4.377f, 
                ["[Giants' Mountaintop Catacombs]"]                                           = 4.357f, 
                ["Defeat 15 enemies in [Academy of Raya Lucaria] (no rest)"]                  = 4.327f, 
                ["Have |X| Axes/Greataxes"]                                                   = 4.317f, 
                ["[Tree Sentinel] (Limgrave)"]                                                = 4.297f, 
                ["Any Ruined Forge"]                                                          = 4.297f, 
                ["Defeat 15 enemies in [Leyndell] (no rest)"]                                 = 4.277f, 
                ["Weeping Evergaol and Lord Contender's Evergaol"]                            = 4.217f, 
                ["Have |X| Hammers/Great Hammers"]                                            = 4.217f, 
                ["Defeat 10 enemies in Caria Manor (no rest)"]                                = 4.187f, 
                ["Make a ring with 5 Rainbow Stones"]                                         = 4.138f, 
                ["[Unsightly Catacombs]"]                                                     = 4.118f, 
                ["Defeat 15 enemies in [Stormveil Castle] (no rest)"]                         = 4.098f, 
                ["[Black Blade Kindred] (Forbidden Lands)"]                                   = 4.068f, 
                ["Touch Church of Dragon Communion Grace"]                                    = 4.058f, 
                ["Defeat 15 enemies in [Crumbling Farum Azula] (no rest)"]                    = 4.038f, 
                ["Parry an attack"]                                                           = 4.028f, 
                ["Any [Ancestor Spirit]"]                                                     = 4.018f, 
                ["All Evergaols in Liurnia (except Ringleader's)"]                            = 4.008f, 
                ["[Ancestor Spirit] and [Regal Ancestor Spirit]"]                             = 3.988f, 
                ["|X| Minor Erdtree bosses (no Torrent)"]                                     = 3.978f, 
                ["[Caelid Catacombs]"]                                                        = 3.948f, 
                ["[Dragonkin Soldier] (Lake of Rot)"]                                         = 3.878f, 
                ["Talk to Tanith (Volcano Manor)"]                                            = 3.798f, 
                ["Defeat an NPC invader in |Region|"]                                         = 3.778f, 
                ["|X| Minor Erdtree bosses"]                                                  = 3.748f, 
                ["Defeat 15 enemies in [Redmane Castle] (no rest)"]                           = 3.708f, 
                ["Collect a Wandering Artist Spirit's reward"]                                = 3.658f, 
                ["Exhaust Wailing Merchant's stock"]                                          = 3.648f, 
                ["Defeat 10 enemies of a caravan"]                                            = 3.438f, 
                ["Malefactor's Evergaol"]                                                     = 3.418f, 
                ["[Magma Wyrm Makar]"]                                                        = 3.398f, 
                ["Exhaust Gelmir Merchant's stock"]                                           = 3.338f, 
                ["Exhaust any merchant's stock"]                                              = 3.318f, 
                ["Loot the Lenne's Rise chest"]                                               = 3.308f, 
                ["Kick the ladder in Church of the Cuckoo (Raya Lucaria)"]                    = 3.278f, 
                ["Hit a dragon with a greased weapon"]                                        = 3.258f, 
                ["Cuckoo's Evergaol"]                                                         = 3.238f, 
                ["[Wormface]"]                                                                = 3.238f, 
                ["Kill [Moonrithyll]"]                                                        = 3.148f, 
                ["+|X| Flasks"]                                                               = 3.118f, 
                ["Defeat 10 enemies in Sellia (no rest)"]                                     = 3.108f, 
                ["[Ancestor Spirit]"]                                                         = 3.098f, 
                ["Stormhill Evergaol"]                                                        = 3.078f, 
                ["[Lansseax]"]                                                                = 3.078f, 
                ["[Godskin Apostle] (Windmill Village)"]                                      = 3.028f, 
                ["Forlorn Hound Evergaol"]                                                    = 3.018f, 
                ["Have a +4 Spirit Ash"]                                                      = 3.008f, 
                ["Defeat the 5 enemies inside the far ruin in Lake of Rot"]                   = 2.978f, 
                ["Loot the Highway Lookout Tower chest (Altus north)"]                        = 2.918f, 
                ["Defeat an Erdtree Avatar"]                                                  = 2.908f, 
                ["Defeat 15 enemies in Mohgwyn Palace (no rest)"]                             = 2.888f, 
                ["Pick up an item on the rafters of Carian Study Hall"]                       = 2.878f, 
                ["Loot the Mirage Rise chest"]                                                = 2.858f, 
                ["Defeat a Silver Sphere"]                                                    = 2.808f, 
                ["[Belurat/Enir-Ilim]"]                                                       = 2.778f, 
                ["Knock on Dung Eater's Gaol Cell Door"]                                      = 2.728f, 
                ["Restore Mohg's Great Rune"]                                                 = 2.718f, 
                ["Light all torches in Ordina Evergaol"]                                      = 2.718f, 
                ["Defeat a Night's Cavalry"]                                                  = 2.688f, 
                ["[Glintstone Dragon Adula]"]                                                 = 2.678f, 
                ["2 Nameless Mausoleum's"]                                                    = 2.658f, 
                ["Loot the Rabbath's Rise puppet"]                                            = 2.648f, 
                ["Restore Morgott's Great Rune"]                                              = 2.618f, 
                ["[Sealed Tunnel]"]                                                           = 2.558f, 
                ["[Shadow Keep]"]                                                             = 2.538f, 
                ["Lux Ruins' boss"]                                                           = 2.518f, 
                ["[Regal Ancestor Spirit]"]                                                   = 2.499f, 
                ["[Tree Spirit] (Stormveil Castle)"]                                          = 2.479f, 
                ["Have |X| Crystal Tears"]                                                    = 2.479f, 
                ["[Cliffbottom Catacombs]"]                                                   = 2.449f, 
                ["Open the only door in Caria Manor"]                                         = 2.449f, 
                ["[Murkwater Catacombs]"]                                                     = 2.439f, 
                ["Clear Caelid Waypoint Ruins interior of enemies"]                           = 2.409f, 
                ["[Road's End Catacombs]"]                                                    = 2.389f, 
                ["[Commander Gaius]"]                                                         = 2.389f, 
                ["Loot both Imp Seal chests in Roundtable Hold"]                              = 2.359f, 
                ["[Black Knife Catacombs]"]                                                   = 2.349f, 
                ["Exhaust Forlorn Merchant's stock"]                                          = 2.339f, 
                ["[Stormfoot Catacombs]"]                                                     = 2.329f, 
                ["Defeat 10 enemies in Village of the Albinaurics (no rest)"]                 = 2.329f, 
                ["Use a Birdseye Telescope"]                                                  = 2.319f, 
                ["Summon 3 different Spirit Ashes"]                                           = 2.309f, 
                ["[Minor Erdtree Catacombs]"]                                                 = 2.299f, 
                ["Kill an enemy in water using lightning damage"]                             = 2.269f, 
                ["Loot the downstairs bed in Roundtable Hold"]                                = 2.249f, 
                ["Use Memory of Grace directly after defeating a boss"]                       = 2.249f, 
                ["[Mohg, Lord of Blood]"]                                                     = 2.229f, 
                ["[Deathtouched Catacombs]"]                                                  = 2.219f, 
                ["Kill 2 invisible Scarabs"]                                                  = 2.199f, 
                ["[Dragonkin Soldier of Nokstella]"]                                          = 2.199f, 
                ["Defeat 10 enemies in the Murkwater ravine (no rest)"]                       = 2.129f, 
                ["Hit a boss with 3 different weapon skills"]                                 = 2.129f, 
                ["Loot the Mimic Tear chest (behind Imp Seal in Nokron)"]                     = 2.119f, 
                ["Use a Baldachin's Blessing during a boss battle"]                           = 2.119f, 
                ["Loot a Spiritgrave ([Larval Tears])"]                                       = 2.079f, 
                ["Caelem Ruins' boss"]                                                        = 2.039f, 
                ["Inflict Frostbite on a boss"]                                               = 2.039f, 
                ["Heal the injured Forager Brood"]                                            = 2.039f, 
                ["Defeat 10 enemies in Scorched Ruins (no rest)"]                             = 2.039f, 
                ["Hit a boss with 3 different Arrows/Bolts"]                                  = 2.029f, 
                ["Kill an invisible Scarab"]                                                  = 2.029f, 
                ["Defeat 10 enemies in Ruins of Unte (no rest)"]                              = 2.019f, 
                ["Kick 3 enemies to their death in Ruin-Strewn Precipice (no rest)"]          = 2.009f, 
                ["Snap your fingers at Varr? (without killing Kal?)"]                         = 2.009f, 
                ["Defeat 10 enemies in Ruin-Strewn Precipice (no rest)"]                      = 1.999f, 
                ["Exhaust Aeonia Merchant's stock"]                                           = 1.989f, 
                ["Defeat 10 enemies around Moorth Ruins (no rest)"]                           = 1.959f, 
                ["Hit a boss with 3 different types of Pots"]                                 = 1.939f, 
                ["Defeat 15 enemies in Castle Ensis (no rest)"]                               = 1.939f, 
                ["Defeat 10 enemies in Frenzied Flame Village (no rest)"]                     = 1.919f, 
                ["Kingsrealm Ruins' boss"]                                                    = 1.919f, 
                ["Defeat 10 enemies in Gatefront Ruins (no rest)"]                            = 1.879f, 
                ["Defeat 10 enemies in Shadow Keep Church District (no rest)"]                = 1.879f, 
                ["Defeat all enemies on the Mohgwyn Palace Dynasty Mausoleum cliff path"]     = 1.869f, 
                ["Sell 80 Mushrooms at once"]                                                 = 1.869f, 
                ["[Lakeside Crystal Cave]"]                                                   = 1.849f, 
                ["Waypoint Ruins' boss"]                                                      = 1.849f, 
                ["Defeat 15 enemies in Stone Coffin Fissure (no rest)"]                       = 1.849f, 
                ["Defeat 15 enemies in [Shadow Keep] (no rest)"]                              = 1.839f, 
                ["Defeat 15 enemies in Bonny Village (no rest)"]                              = 1.829f, 
                ["Learn |X| Incantations"]                                                    = 1.819f, 
                ["[Academy Crystal Cave]"]                                                    = 1.809f, 
                ["Loot the bell tower chest in Raya Lucaria (beyond [Academy Crystal Cave])"] = 1.799f, 
                ["Exhaust West Peninsula Merchant's stock"]                                   = 1.799f, 
                ["Defeat 10 enemies in Prospect Town (no rest)"]                              = 1.799f, 
                ["60 Strength"]                                                               = 1.769f, 
                ["Defeat 10 enemies in Temple Town Ruins (no rest)"]                          = 1.769f, 
                ["Fell 2 Mausoleums in different regions"]                                    = 1.759f, 
                ["Have |X| Talismans"]                                                        = 1.759f, 
                ["Learn |X| Sorceries"]                                                       = 1.759f, 
                ["Defeat 15 enemies in Village of Flies (no rest)"]                           = 1.749f, 
                ["Clear the Morne Rampart swamp of enemies"]                                  = 1.729f, 
                ["[Fia's Champions]"]                                                         = 1.729f, 
                ["Defeat 15 enemies in Fort Reprimand (no rest)"]                             = 1.729f, 
                ["Defeat 10 enemies around Cathedral of Manus Metyr (no rest)"]               = 1.729f, 
                ["Both Evergaols in Limgrave"]                                                = 1.719f, 
                ["Help Alexander (Limgrave)"]                                                 = 1.689f, 
                ["Defeat 15 enemies in [Belurat/Enir-Ilim] (no rest)"]                        = 1.689f, 
                ["30 |Attribute1| and |Attribute2|"]                                          = 1.669f, 
                ["Defeat 15 enemies in Rauh Ruins (no rest)"]                                 = 1.669f, 
                ["Purchase a Dragon Heart-costing item"]                                      = 1.659f, 
                ["Discard a Remembrance or Hero's/Lord's Rune"]                               = 1.629f, 
                ["[Ruined Forge Lava Intake]"]                                                = 1.609f, 
                ["Loot Perfumer's Ruins chest"]                                               = 1.599f, 
                ["[Raya Lucaria Crystal Tunnel]"]                                             = 1.549f, 
                ["[Scorpion River Catacombs]"]                                                = 1.549f, 
                ["Ringleader's Evergaol"]                                                     = 1.509f, 
                ["[Taylew's Ruined Forge]"]                                                   = 1.509f, 
                ["2 Bosses in Dragon's Pit Terminus/Jagged Peak"]                             = 1.499f, 
                ["[Spiritcaller Cave]"]                                                       = 1.489f, 
                ["Loot the Smouldering Church"]                                               = 1.469f, 
                ["60 Dexterity"]                                                              = 1.459f, 
                ["[Belurat Gaol]"]                                                            = 1.459f, 
                ["[Yelough Anix Tunnel]"]                                                     = 1.449f, 
                ["50 |Attribute1|"]                                                           = 1.449f, 
                ["[Ruined Forge of Starfall Past]"]                                           = 1.449f, 
                ["Eastern Nameless Mausoleum"]                                                = 1.449f, 
                ["[Mimic Tear]"]                                                              = 1.439f, 
                ["Loot the Oridys's Rise chest"]                                              = 1.419f, 
                ["Loot Wyndham Ruins chest"]                                                  = 1.419f, 
                ["Defeat 2 Nameless White Masks (Mohgwyn Palace)"]                            = 1.419f, 
                ["[Rivermouth Cave]"]                                                         = 1.419f, 
                ["Visit Three Fingers"]                                                       = 1.399f, 
                ["[Fog Rift Catacombs]"]                                                      = 1.399f, 
                ["[Darklight Catacombs]"]                                                     = 1.399f, 
                ["[Sage's Cave]"]                                                             = 1.389f, 
                ["Loot the Heretical Rise chest"]                                             = 1.389f, 
                ["[Perfumer's Grotto]"]                                                       = 1.379f, 
                ["Defeat 10 enemies in Night's Sacred Ground (no rest)"]                      = 1.349f, 
                ["Visit the Artist's Shack in Limgrave"]                                      = 1.349f, 
                ["[Dragon's Pit]"]                                                            = 1.349f, 
                ["Visit The Towering Sister"]                                                 = 1.339f, 
                ["[Lamenter's Gaol]"]                                                         = 1.339f, 
                ["[Volcano Cave]"]                                                            = 1.319f, 
                ["[Gaol Cave]"]                                                               = 1.319f, 
                ["[Old Altus Tunnel]"]                                                        = 1.319f, 
                ["Visit Aureliette (Spirit Jellyfish)"]                                       = 1.309f, 
                ["[Earthbore Cave]"]                                                          = 1.289f, 
                ["Hit a boss with a throwing pot"]                                            = 1.289f, 
                ["[Altus Tunnel]"]                                                            = 1.279f, 
                ["Loot the Forest Lookout Tower chest"]                                       = 1.279f, 
                ["[Stillwater Cave]"]                                                         = 1.269f, 
                ["Touch Jarburg Grace"]                                                       = 1.269f, 
                ["Use 4 Golden Rune [4+] at once"]                                            = 1.269f, 
                ["[Morne Tunnel]"]                                                            = 1.259f, 
                ["Loot Caelid Waypoint Ruins chest"]                                          = 1.239f, 
                ["[Bonny Gaol]"]                                                              = 1.239f, 
                ["Defeat 10 enemies in Carian Study Hall (no rest)"]                          = 1.229f, 
                ["Southern Nameless Mausoleum"]                                               = 1.229f, 
                ["[Seethewater Cave]"]                                                        = 1.219f, 
                ["Western Nameless Mausoleum"]                                                = 1.219f, 
                ["Use 5 Golden Rune [3+] at once"]                                            = 1.209f, 
                ["Any Nameless Mausoleum"]                                                    = 1.209f, 
                ["[Groveside Cave]"]                                                          = 1.199f, 
                ["Writheblood Ruins' boss"]                                                   = 1.189f, 
                ["Loot Purified Ruins chest"]                                                 = 1.189f, 
                ["Loot the Converted Tower chest (Liurnia)"]                                  = 1.179f, 
                ["Loot the Albinauric Rise chest"]                                            = 1.179f, 
                ["Stop the Frenzied Flame in the Frenzy-Flaming Tower"]                       = 1.159f, 
                ["Loot Forsaken Ruins chest"]                                                 = 1.159f, 
                ["[Highroad Cave]"]                                                           = 1.149f, 
                ["Loot the Highway Lookout Tower chest (Liurnia east)"]                       = 1.139f, 
                ["Use the waygate next to the Fingerslayer Blade chest"]                      = 1.119f, 
                ["Loot Demi-Human Forest Ruins chest"]                                        = 1.109f, 
                ["Northern Nameless Mausoleum"]                                               = 1.109f, 
                ["[Gael Tunnel]"]                                                             = 1.099f, 
                ["Loot the Church of Inhibition"]                                             = 1.099f, 
                ["Defeat 15 enemies within the walls of Shaded Castle (no rest)"]             = 1.089f, 
                ["Defeat 10 enemies in Ailing Village (no rest)"]                             = 1.079f, 
                ["Have |X| Cookbooks"]                                                        = 1.079f, 
                ["[Abandoned Cave]"]                                                          = 1.069f, 
                ["[Sellia Hideaway]"]                                                         = 1.059f, 
                ["Exhaust Pidia's stock"]                                                     = 1.059f, 
                ["Exhaust Writheblood Merchant's stock"]                                      = 1.059f, 
                ["[Sellia Crystal Tunnel]"]                                                   = 1.049f, 
                ["Defeat 15 enemies in Subterranean Shunning-Grounds (no rest)"]              = 1.039f, 
                ["Defeat 15 enemies in Castle Sol (no rest)"]                                 = 1.039f, 
                ["Loot Street of Sages Ruins chest"]                                          = 1.039f, 
                ["Loot Witchbane Ruins"]                                                      = 1.039f, 
                ["Receive power from a Remembrance"]                                          = 1.029f, 
                ["Defeat 15 enemies in Uhl Palace Ruins (no rest) (Ainsel River)"]            = 1.019f, 
                ["[Limgrave Tunnels]"]                                                        = 1.009f, 
                ["[Dragonbarrow Cave]"]                                                       = 0.989f, 
                ["Have |X| Shields"]                                                          = 0.979f, 
                ["Loot 2 chests in Fort of Reprimand"]                                        = 0.969f, 
                ["[Cave of Knowledge]"]                                                       = 0.959f, 
                ["Loot Gatefront Ruins chest"]                                                = 0.959f, 
                ["Loot the Scorched Ruins chest"]                                             = 0.959f, 
                ["Exhaust Lake Shore Merchants stock"]                                        = 0.939f, 
                ["Touch the Charo's Hidden Grave Grace"]                                      = 0.939f, 
                ["Fell 2 Liurnia Mausoleums"]                                                 = 0.929f, 
                ["Attain Rebirth"]                                                            = 0.919f, 
                ["Loot Yelough Anix Ruins"]                                                   = 0.909f, 
                ["Loot the Ruins of Unte chest"]                                              = 0.909f, 
                ["Loot a Giant Stone Coffin (Cerulean Coast)"]                                = 0.909f, 
                ["Fell the Apostate Derelict Mausoleum"]                                      = 0.899f, 
                ["Loot Laskyar Ruins chest"]                                                  = 0.899f, 
                ["Loot the Temple Town Ruins chest"]                                          = 0.899f, 
                ["Loot 3 chests in Castle Ensis"]                                             = 0.899f, 
                ["Use 6 Golden Rune [2+] at once"]                                            = 0.889f, 
                ["Loot the Prospect Town chest"]                                              = 0.879f, 
                ["Fell the Weeping Peninsula Mausoleum"]                                      = 0.869f, 
                ["Fell the Deeproot Depths Mausoleum"]                                        = 0.869f, 
                ["Loot the Church of Benediction"]                                            = 0.859f, 
                ["[Murkwater Cave]"]                                                          = 0.849f, 
                ["Loot the highest chest in Nokstella ([Moon of Nokstella])"]                 = 0.849f, 
                ["Loot Dragon-Burnt Ruins chest"]                                             = 0.849f, 
                ["Loot the Suppressing Pillar chest"]                                         = 0.849f, 
                ["Loot Rabbath's Rise chest"]                                                 = 0.849f, 
                ["Have 150 Rowa Fruit"]                                                       = 0.839f, 
                ["Loot the Moorth Ruins chest"]                                               = 0.810f, 
                ["Purchase an item at the Grand Altar of Dragon Communion"]                   = 0.800f, 
                ["Loot Tombsward Ruins"]                                                      = 0.790f, 
                ["[Valiant Gargoyles]"]                                                       = 0.790f, 
                ["Use 3 Golden Rune [5+] at once"]                                            = 0.730f, 
                ["Loot the Illusory Tree in Nokstella"]                                       = 0.720f, 
                ["Loot Mistwood Ruins chest"]                                                 = 0.710f, 
                ["Exhaust Mistwood Merchant's stock"]                                         = 0.710f, 
                ["Exhaust Moore's stock"]                                                     = 0.710f, 
                ["Use Starlight Shards during a boss fight"]                                  = 0.690f, 
                ["Fell the Castle Sol Mausoleum"]                                             = 0.690f, 
                ["Give a Prayerbook or Scroll to an npc"]                                     = 0.690f, 
                ["Eat 2 Cured Meat/Dried Liver"]                                              = 0.670f, 
                ["Exhaust Dragonbarrow Merchant's stock"]                                     = 0.660f, 
                ["Craft 2 types of unfletched Arrows"]                                        = 0.650f, 
                ["Eat 2 types of Boluses"]                                                    = 0.650f, 
                ["Defeat 6 enemies in/on Divine Tower of Caelid (no rest)"]                   = 0.630f, 
                ["Loot 3 chests in Nokstella"]                                                = 0.630f, 
                ["Have 15 Trina's Lily"]                                                      = 0.630f, 
                ["Craft 2 types of Bolts"]                                                    = 0.610f, 
                ["Craft any Perfume"]                                                         = 0.580f, 
                ["[Astel, Naturalborn of the Void]"]                                          = 0.570f, 
                ["Have a +6 Spirit Ash"]                                                      = 0.560f, 
                ["Have 6 Spirit Ashes"]                                                       = 0.560f, 
                ["Exhaust Kal?'s stock"]                                                      = 0.490f, 
                ["Duplicate a Remembrance"]                                                   = 0.420f, 
                ["Craft 2 types of Grease"]                                                   = 0.390f, 
                ["Loot Perfumer's, Wyndham, and Lux Ruins chests"]                            = 0.270f, 
                ["Restore 2 Great Runes"]                                                     = 0.000f, 
                ["Hero's Graves in two different regions"]                                    = 0.000f, 
                ["|X| Ruin bosses"]                                                           = 0.000f, 
                ["Touch an overworld Grace in 4 regions"]                                     = 0.000f, 
                ["Touch an overworld Grace in 5 regions"]                                     = 0.000f, 
                ["3 Nameless Mausoleum's"]                                                    = 0.000f, 
                ["Collect a Land of Shadow Wandering Artist Spirit's reward"]                 = 0.000f, 
            };

            Console.Clear();

            List<string> AllGoalsStr = new List<string>(File.ReadAllLines(@"X:\ER Modding\ModEngine-2.0.0-preview3-win64\randomizerRace\Data\GoalList.json"));

            bool isNewGoal = false;
            string currentGoalName = "";
            string[] goalCharsToRemove = new string[] { "[", "]", "(", ")", "|", " ", "-", "'" };

            for (int i = 0; i < AllGoalsStr.Count; i++)
            {
                if (AllGoalsStr[i].Contains("{"))
                {
                    isNewGoal = true;
                    continue;
                }

                if (isNewGoal)
                {
                    if (AllGoalsStr[i].Contains("LocationBasic"))
                    {
                        string goalDesc = "";
                        int indexOfFirstColon = AllGoalsStr[i].IndexOf(':');
                        for (int ci = indexOfFirstColon + 3; ci < AllGoalsStr[i].Length; ci++)
                        {
                            if (AllGoalsStr[i][ci] != '"')
                                goalDesc += AllGoalsStr[i][ci];
                            else
                                break;
                        }

                        string locationBasic = "";
                        int indexOfSecondColon = AllGoalsStr[i].IndexOf(':', AllGoalsStr[i].IndexOf(':') + 1);
                        for (int ci = indexOfSecondColon + 3; ci < AllGoalsStr[i].Length; ci++)
                        {
                            if (AllGoalsStr[i][ci] != '"')
                                locationBasic += AllGoalsStr[i][ci];
                            else
                                break;
                        }
                        currentGoalName = goalDesc;
                        if (locationBasic != "None")
                            currentGoalName = goalDesc + " (" + locationBasic + ")";

                        string internalName = goalDesc;
                        foreach (var gctm in goalCharsToRemove)
                            internalName = internalName.Replace(gctm, string.Empty);
                        if (locationBasic != "None")
                            internalName += locationBasic.Replace(" ", "");
                        AllGoalsStr[i] = AllGoalsStr[i].Replace(", \"LocationBasic\":", ", \"InternalName\": " + "\"" + internalName + "\", \"LocationBasic\":");
                    }
                    else if (AllGoalsStr[i].Contains("BonusProbability"))
                    {
                        float DistValue = 0.0f;
                        if (GoalDistributions.ContainsKey(currentGoalName))
                            DistValue = GoalDistributions[currentGoalName];
                        else
                            Trace.WriteLine("Could not find goal " + currentGoalName + " in goal distribution list, assigned value 0.0");

                        AllGoalsStr[i] = AllGoalsStr[i].Replace(", \"BonusProbability\":", ", \"DistributionValue\": " + DistValue.ToString().Replace(',', '.') + ", \"BonusProbability\":");
                        isNewGoal = false;
                    }
                }
            }

            File.WriteAllLines(@"X:\ER Modding\ModEngine-2.0.0-preview3-win64\randomizerRace\Data\GoalListUpdated.json", AllGoalsStr);
        }
    }
}
