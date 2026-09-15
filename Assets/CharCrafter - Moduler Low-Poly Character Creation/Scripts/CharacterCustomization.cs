namespace AyuoDev.CharCrafter
{
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Collections;
    using System.IO;

    /// <summary>
    /// Manages character customization logic including clothing toggling and material changes at runtime.
    /// Saving Prefab-Ready to Use Character's for your game
    /// </summary>
    public class CharacterCustomization : MonoBehaviour
    {
        [Header("Character Creation")]
        public MeshContainer[] GenderInformation;
        public int SelectedGender;
        private int SelectedCatagory;
        public PresetCollection Presets;
        private RandomserControlParamater randomiser;
        #region Saving History 
        // every thing here is controlled internaly do not touch :D
        private List<History> ChangeHistory = new List<History>();
        private int CurrentHistoryIndex = 0;
        private bool SavingHistory;
        #endregion

        #region Color Changer Variables
        [Header("Color Changer")]
        [NonSerialized] public CustomizableObject SelectedObject;
        [NonSerialized] public int SelectedMaterialSlot;
        public Material[] SkinColors;
        public Material[] ColorMaterials;
        public Material[] GlassMaterial;

        #endregion

        #region UI Handler Variables

        [Header("Models Info")]
        public GameObject[] GeneralMenus;
        public GameObject[] CustomizeMenus;
        public Image[] CustomizMenuButtons;
        public Transform AppearanceContent;
        public GameObject AppearancePrefab;
        private List<Text> ModelName = new List<Text>();
        private List<Text> ClothAmount = new List<Text>();
        [Header("Controlled Randomiser")]
        public GameObject SectionSettingPrefab;
        public Transform RandomiserContent;
        public GameObject ClothRandomiserPrefab;
        private List<GameObject> randomiserClothingSection = new List<GameObject>();
        private List<GameObject> randomiserMorphSection = new List<GameObject>();
        public Sprite ClosedArrow;
        public Sprite OpenArrow;
        [Header("Body Parts Section")]
        public Transform BodyPartsContent;
        [Header("Closet UI")]
        public GameObject ClosetUI;
        public Transform ClosetContent;
        public GameObject ItemHolderUI;
        public Sprite NoneIcon;
        public Text ClosetTitle;
        public Image ClosetIcon;
        public Sprite[] ClothIcons;
        public Color SelectedClothIndex;
        public Color NormalClothIndex;
        private List<Image> SpawnedButtons = new List<Image>();
        [Header("Color Closet UI")]
        public GameObject[] HeadColorsBut;
        public GameObject ColorClosetUI;
        public Transform ColorContent;
        public GameObject ColorKnob;
        public Color SelectedHeadColor;
        public Color NormalHeadColor;
        [Header("Skin Color")]
        public Transform SkinColorContent;
        [Header("Mesh Model Types")]
        public Transform BaseModelContent;
        public GameObject BaseModelPrefab;
        [Header("Presets UI")]
        public Transform PresetContent;
        public GameObject PresetPrefab;
        public GameObject SavePresetMenu;
        public Text SavePresetTxt;
        [Header("Blend Keys UI")]
        public Transform ShapeKeysContent;
        public GameObject ShapeKeysPrefab;
        #endregion

        void Start()
        {
            ChangeGender(0);
            PrepareAppearance();
            IntilizeGenders();
            SelectCatagory(0);
            GetSkinColors();
            PrepareBaseModels();
            SwapMenu(0);
            PreparePresets();
            SavingHistory = true;
        }
        #region Save Object as a Prefab in your project Directory
        public void SaveCleanedCharacter()
        {
#if UNITY_EDITOR
            if (GenderInformation[SelectedGender].GenderBaseMesh == null)
            {
                Debug.LogError("No character assigned.");
                return;
            }

            GameObject characterCopy = Instantiate(GenderInformation[SelectedGender].GenderBaseMesh);
            characterCopy.name = GenderInformation[SelectedGender].GenderBaseMesh.name;

            DestroyImmediate(characterCopy.GetComponent<MeshContainer>());
            DeleteInactiveChildren(characterCopy.transform);

            string folderPath = "Assets/Prefabs/Characters";
            string baseName = characterCopy.name;
            string extension = ".prefab";
            string finalPath = folderPath + "/" + baseName + extension;
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Characters");

            int count = 1;
            while (System.IO.File.Exists(finalPath))
            {
                finalPath = folderPath + "/" + baseName + "_" + count + extension;
                count++;
            }
            PrefabUtility.SaveAsPrefabAsset(characterCopy, finalPath);
            Debug.Log("Character saved to: " + finalPath);
            DestroyImmediate(characterCopy);
#endif
        }
        private void DeleteInactiveChildren(Transform parent)
        {
            if(parent.GetComponent<MeshContainer>()) Destroy(parent.GetComponent<MeshContainer>());
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);

                if (!child.gameObject.activeSelf)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
        #endregion

        #region Reseting All Valus Function / Button

        public void HardReset()
        {
            MeshContainer currentGender = GenderInformation[SelectedGender];

            for (int i = 0; i < currentGender.ClothingOptions.Length; i++)
            {
                if (currentGender.ClothingOptions[i].ClothingObjects.Length > 0)
                {
                    CatagoryDetail currentCategory = currentGender.ClothingOptions[i];
                    ResetClothes(currentCategory, keepIndex: (currentCategory.CatagoryName.ToString() == "TopClothes" || currentCategory.CatagoryName.ToString() == "BottomClothes") ? 0 : -1);

                    // Update UI Texts
                    if (currentCategory.CatagoryIndex >= 0)
                        ModelName[i].text = currentCategory.ClothingObjects[0].model.name;
                    else
                        ModelName[i].text = "None";

                    ClothAmount[i].text = $"{currentCategory.CatagoryIndex + 1} / {currentCategory.ClothingObjects.Length}";
                }
            }

            for(int x = 0; x < currentGender.BlendShapes.Length;x++)
            {
                BlendShapeSLider(x, 0f);
            }
            if (SavingHistory) RecordNewChange();
        }
        private void ResetClothes(CatagoryDetail category, int keepIndex)
        {
            for (int i = 0; i < category.ClothingObjects.Length; i++)
            {
                if (category.ClothingObjects[i] != null && category.ClothingObjects[i].model != null)
                {
                    category.ClothingObjects[i].model.SetActive(i == keepIndex);
                }
            }
            category.CatagoryIndex = keepIndex;
        }
        public void IntilizeGenders()
        {
            for (int x = 0; x < GenderInformation.Length; x++)
            {
                MeshContainer currentGender = GenderInformation[x];

                for (int i = 0; i < currentGender.ClothingOptions.Length; i++)
                {
                    if (currentGender.ClothingOptions[i].ClothingObjects.Length > 0)
                    {
                        CatagoryDetail currentCategory = currentGender.ClothingOptions[i];
                        ResetClothes(currentCategory, keepIndex: (currentCategory.CatagoryName.ToString() == "TopClothes" || currentCategory.CatagoryName.ToString() == "BottomClothes") ? 0 : -1);

                        // Update UI Texts
                        if (currentCategory.CatagoryIndex >= 0)
                            ModelName[i].text = currentCategory.ClothingObjects[0].model.name;
                        else
                            ModelName[i].text = "None";

                        ClothAmount[i].text = $"{currentCategory.CatagoryIndex + 1} / {currentCategory.ClothingObjects.Length}";
                    }
                }
                for (int j = 0; j < currentGender.BlendShapes.Length; j++)
                {
                    BlendShapeSLider(j, 0f);
                }
            }
            InitializeRandomColorsForAllClothing();
            RecordNewChange();
        }
        public void InitializeRandomColorsForAllClothing()
        {
            for (int g = 0; g < GenderInformation.Length; g++)
            {
                MeshContainer gender = GenderInformation[g];

                foreach (var category in gender.ClothingOptions)
                {
                    foreach (var clothing in category.ClothingObjects)
                    {
                        if (clothing.model == null) continue;

                        Renderer rend = clothing.model.GetComponent<Renderer>();
                        if (rend == null) continue;

                        Material[] mats = rend.sharedMaterials;
                        clothing.CurrentIndex = new int[clothing.materialSlotNames.Length];

                        for (int slot = 0; slot < clothing.materialSlotNames.Length; slot++)
                        {
                            int matCount = GetMaterialCountForType(clothing.materialSlotNames[slot]);
                            int randomIndex = UnityEngine.Random.Range(0, matCount);
                            clothing.CurrentIndex[slot] = randomIndex;

                            Material chosenMat = GetMaterialByTypeAndIndex(clothing.materialSlotNames[slot], randomIndex);
                            if (slot < mats.Length && chosenMat != null)
                            {
                                mats[slot] = chosenMat;
                            }
                        }

                        rend.sharedMaterials = mats;
                    }
                }
            }
        }

        #endregion

        #region Randomizer Functions
        /// <summary>
        /// Calls Randomize Clothes Function for each array .
        /// Also adjusts the UI for each Category.
        /// </summary>
        public void Randomizer()
        {
            MeshContainer currentGender = GenderInformation[SelectedGender];

            // Randomize all clothing categories
            for (int catIndex = 0; catIndex < currentGender.ClothingOptions.Length; catIndex++)
            {
                CatagoryDetail currentCategory = currentGender.ClothingOptions[catIndex];
                int totalClothes = currentCategory.ClothingObjects.Length;

                // If there are no clothing objects in this category, skip it entirely
                if (totalClothes == 0)
                {
                    currentCategory.CatagoryIndex = -1;
                    ModelName[catIndex].text = "None";
                    ClothAmount[catIndex].text = "0/0";
                    continue;
                }

                // Randomly select -1 (None) or one of the clothing objects
                int randomIndex = UnityEngine.Random.Range(-1, totalClothes);
                currentCategory.CatagoryIndex = randomIndex;

                // Activate/deactivate models
                for (int i = 0; i < totalClothes; i++)
                {
                   currentCategory.ClothingObjects[i].model.SetActive(i == randomIndex); 
                }

                // If a clothing piece was selected, randomize its materials
                if (randomIndex != -1)
                {
                    CustomizableObject selectedObject = currentCategory.ClothingObjects[randomIndex];
                    selectedObject.CurrentIndex = new int[selectedObject.materialSlotNames.Length];

                    Renderer rend = selectedObject.model.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        Material[] mats = rend.sharedMaterials;

                        for (int slot = 0; slot < selectedObject.materialSlotNames.Length; slot++)
                        {
                            int materialCount = GetMaterialCountForType(selectedObject.materialSlotNames[slot]);
                            int randomMaterialIndex = UnityEngine.Random.Range(0, materialCount);
                            selectedObject.CurrentIndex[slot] = randomMaterialIndex;

                            Material chosenMat = GetMaterialByTypeAndIndex(selectedObject.materialSlotNames[slot], randomMaterialIndex);
                            if (slot < mats.Length && chosenMat != null)
                            {
                                mats[slot] = chosenMat;
                            }
                        }

                        rend.sharedMaterials = mats;
                    }
                }

                // Update UI
                if (randomIndex == -1)
                {
                    ModelName[catIndex].text = "None";
                    ClothAmount[catIndex].text = $"0/{totalClothes}";
                }
                else
                {
                    ModelName[catIndex].text = currentCategory.ClothingObjects[randomIndex].model.name;
                    ClothAmount[catIndex].text = $"{randomIndex + 1}/{totalClothes}";
                }

                if (currentCategory.CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                {
                    int hatIndex = currentCategory.CatagoryIndex;

                    if (hatIndex >= 0)
                    {
                        var hat = currentCategory.ClothingObjects[hatIndex];
                        ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                    }
                    else
                    {
                        ApplyHatShapeKey(0f); // No hat remove hair deformation
                    }
                }
            }

            // Randomize skin color
            if (SkinColors.Length > 0)
            {
                int randomSkinIndex = UnityEngine.Random.Range(0, SkinColors.Length);
                ChangeSkinColor(randomSkinIndex);
            }

            // Randomize ShapeKeys
            for (int i = 0; i < currentGender.BlendShapes.Length; i++)
            {
                float shapekeyvalue = UnityEngine.Random.Range(0f, 100f);
                BlendShapeSLider(i, shapekeyvalue);
            }

            // Reset UI state
            SelectedObject = null;
            ColorClosetUI.SetActive(false);
            ClosetUI.SetActive(false);
            if (SavingHistory) RecordNewChange();
        }

        /// <summary>
        /// Gets the number of available materials for a given material slot type.
        /// </summary>
        /// <param name="type">The name of the material slot.</param>
        /// <returns>Number of available materials for this type.</returns>
        private int GetMaterialCountForType(CustomizableObject.MaterialType type)
        {
            switch (type)
            {
                case CustomizableObject.MaterialType.Normal:
                    return ColorMaterials.Length;
                case CustomizableObject.MaterialType.SeeThrough:
                    return GlassMaterial.Length;
                default:
                    return 0;
            }
        }
        private Material GetMaterialByTypeAndIndex(CustomizableObject.MaterialType type, int index)
        {
            switch (type)
            {
                case CustomizableObject.MaterialType.Normal:
                    return (index >= 0 && index < ColorMaterials.Length) ? ColorMaterials[index] : null;
                case CustomizableObject.MaterialType.SeeThrough:
                    return (index >= 0 && index < GlassMaterial.Length) ? GlassMaterial[index] : null;
                default:
                    return null;
            }
        }
        #endregion

        #region Controlled Randomiser
        public void ControlledRandomiser()
        {
            MeshContainer currentGender = GenderInformation[SelectedGender];

            // Safety check: initialize Clothing list if needed
            if (randomiser.Clothing == null || randomiser.Clothing.Count != currentGender.ClothingOptions.Length)
            {
                Debug.LogWarning("randomiser.Clothing is not initialized or size mismatch. Initializing all true by default.");
                randomiser.Clothing = Enumerable.Repeat(true, currentGender.ClothingOptions.Length).ToList();
            }

            // Loop over all clothing categories
            for (int catIndex = 0; catIndex < currentGender.ClothingOptions.Length; catIndex++)
            {
                // Only randomize if toggle is true
                if (!randomiser.Clothing[catIndex])
                {
                    // Deactivate all clothes in this category and update UI
                    CatagoryDetail cat = currentGender.ClothingOptions[catIndex];
                    cat.CatagoryIndex = -1;

                    foreach (var obj in cat.ClothingObjects)
                        obj.model.SetActive(false);

                    ModelName[catIndex].text = "None";
                    ClothAmount[catIndex].text = "0/0";
                    if (cat.CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                        ApplyHatShapeKey(0f);
                    continue; // skip randomizing this category
                }

                // Randomize this category (copy from your original Randomizer)
                CatagoryDetail category = currentGender.ClothingOptions[catIndex];
                int totalClothes = category.ClothingObjects.Length;

                if (totalClothes == 0)
                {
                    category.CatagoryIndex = -1;
                    ModelName[catIndex].text = "None";
                    ClothAmount[catIndex].text = "0/0";
                    continue;
                }

                int randomIndex = UnityEngine.Random.Range(-1, totalClothes);
                category.CatagoryIndex = randomIndex;

                for (int i = 0; i < totalClothes; i++)
                    category.ClothingObjects[i].model.SetActive(i == randomIndex);

                if (randomIndex == -1)
                {
                    ModelName[catIndex].text = "None";
                    ClothAmount[catIndex].text = $"0/{totalClothes}";
                }
                else
                {
                    ModelName[catIndex].text = category.ClothingObjects[randomIndex].model.name;
                    ClothAmount[catIndex].text = $"{randomIndex + 1}/{totalClothes}";
                }

                if (category.CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                {
                    int hatIndex = category.CatagoryIndex;

                    if (hatIndex >= 0)
                    {
                        var hat = category.ClothingObjects[hatIndex];
                        ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                    }
                    else
                    {
                        ApplyHatShapeKey(0f);
                    }
                }
            }

            // Randomize ShapeKeys
            for (int i = 0; i < currentGender.BlendShapes.Length; i++)
            {
                if (randomiser.BodyMorph[i] == false) continue;
                float shapekeyvalue = UnityEngine.Random.Range(0f, 100f);
                BlendShapeSLider(i, shapekeyvalue);
            }

            // Optional: you can skip skin and shape keys here or keep them
            SelectedObject = null;
            ColorClosetUI.SetActive(false);
            ClosetUI.SetActive(false);
            if (SavingHistory) RecordNewChange();
        }


        public void ClothingToggle(bool value , int index)
        {
            randomiser.Clothing[index] = value;
        }
        public void BodyMorphToggle(bool value , int index)
        {
            randomiser.BodyMorph[index] = value;
        }
        public void ClothingSectionState(Image Icon)
        {
            bool state = !randomiserClothingSection[0].activeSelf;
            Icon.sprite = (state) ? OpenArrow : ClosedArrow;
            for(int i = 0; i < randomiserClothingSection.Count;i++)
            {
                randomiserClothingSection[i].SetActive(state);
            }
        }
        public void BodyMorphSectionState(Image Icon)
        {
            bool state = !randomiserMorphSection[0].activeSelf;
            Icon.sprite = (state) ? OpenArrow : ClosedArrow;
            for (int i = 0; i < randomiserMorphSection.Count; i++)
            {
                randomiserMorphSection[i].SetActive(state);
            }
        }
        #endregion

        #region Skin Tone Functions
        /// <summary>
        /// this Function is impleneted inside The Skin Color UI Buttons to select color index for the skin ton
        /// </summary>
        public void ChangeSkinColor(int newIndex)
        {

            if (newIndex < 0 || newIndex >= SkinColors.Length)
            {
                Debug.LogWarning("Invalid skin color index.");
                return;
            }

            // Update currentSkinColor for this gender
            GenderInformation[SelectedGender].currentSkinColor = newIndex;

            // Update material on the gender's base mesh renderer
            if (GenderInformation[SelectedGender].GenderBaseRenderer != null)
            {
                Material[] mats = GenderInformation[SelectedGender].GenderBaseRenderer.sharedMaterials;
                mats[0] = SkinColors[newIndex];
                GenderInformation[SelectedGender].GenderBaseRenderer.sharedMaterials = mats;
            }
            else
            {
                Debug.LogWarning("GenderBaseRenderer is null for selected gender.");
            }

            for (int i = 0; i < GenderInformation[SelectedGender].BodyShape.Length; i++)
            {
                Material[] mats = GenderInformation[SelectedGender].BodyShape[i].BodyPart.GetComponent<SkinnedMeshRenderer>().sharedMaterials;
                mats[0] = SkinColors[newIndex];
                GenderInformation[SelectedGender].BodyShape[i].BodyPart.GetComponent<SkinnedMeshRenderer>().sharedMaterials = mats;
            }

            // Update any UI or visuals that depend on skin color
            HeadColors();
            if (SavingHistory) RecordNewChange();
        }
        /// <summary>
        /// Called at the start of the scene to create every tone skin Color if you want add more skin colors make sure to add them outside of run-time
        /// </summary>
        public void GetSkinColors()
        {
            if (SkinColors.Length <= 0) return;
            foreach (Transform child in SkinColorContent) GameObject.Destroy(child.gameObject);

            int i = 0;
            
            foreach (Material col in SkinColors)
            {
                GameObject NewHolder = Instantiate(ColorKnob, SkinColorContent);
                NewHolder.transform.Find("Color").GetComponent<Image>().color = col.color;

                int capturedIndex = i;
                NewHolder.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ChangeSkinColor(capturedIndex);
                });

                i++;
            }
        }
        #endregion

        #region UI Content Visibility Functions
        /// <summary>
        /// Controls which Menu is visible from the control menu buttons such as (Base Model , Customize ...)
        /// </summary>
        public void SwapMenu(int index)
        {
            for(int i = 0; i < GeneralMenus.Length;i++)
            {
                GeneralMenus[i].SetActive(index == i);
            }
        }
        /// <summary>
        /// Controls which Menu is visible from the customization Menu such as ( Clothing , Body Parts , RandomCustomizations)
        /// </summary>
        public void SwapCustomizeMenu(int index)
        {
            for (int i = 0; i < CustomizeMenus.Length; i++)
            {
                CustomizeMenus[i].SetActive(index == i);
                CustomizMenuButtons[i].color = (index == i) ? Color.red : Color.white;
            }
        }
        /// <summary>
        /// this function is called every time we move back to the Customize Menu to display the needed data depending on which base model we select
        /// </summary>
        public void PrepareAppearance()
        {
            foreach (Transform child in AppearanceContent)
                Destroy(child.gameObject);

            foreach (Transform child in RandomiserContent)
                Destroy(child.gameObject);

            foreach (Transform child in BodyPartsContent)
                Destroy(child.gameObject);

            randomiserClothingSection.Clear();
            randomiser = new RandomserControlParamater();
            randomiser.Clothing = Enumerable.Repeat(true, GenderInformation[SelectedGender].ClothingOptions.Length).ToList();
            randomiser.BodyMorph = Enumerable.Repeat(true, GenderInformation[SelectedGender].BlendShapes.Length).ToList();
            ModelName.Clear();
            ClothAmount.Clear();

            GameObject SectionDevider = Instantiate(SectionSettingPrefab, RandomiserContent);
            SectionDevider.transform.Find("section").GetComponent<Text>().text = "Cloth Section";
            SectionDevider.GetComponent<Button>().onClick.AddListener(() => ClothingSectionState(SectionDevider.transform.Find("icon").GetComponent<Image>()));


            for (int i = 0; i < GenderInformation[SelectedGender].ClothingOptions.Length; i++)
            {
                if (GenderInformation[SelectedGender].ClothingOptions[i].ClothingObjects.Length > 0)
                {
                    GameObject newContent = Instantiate(AppearancePrefab, AppearanceContent);
                    int capturedIndex = i;

                    ModelName.Add(newContent.transform.Find("Object_Name").GetComponent<Text>());
                    ClothAmount.Add(newContent.transform.Find("List_Amount").GetComponent<Text>());

                    newContent.transform.Find("Object_Name").GetComponent<Text>().text = (GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIndex >= 0) ? GenderInformation[SelectedGender].ClothingOptions[i].ClothingObjects[GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIndex].model.name : "none";
                    newContent.transform.Find("List_Amount").GetComponent<Text>().text = $"{GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIndex + 1}/{GenderInformation[SelectedGender].ClothingOptions[i].ClothingObjects.Length}";

                    newContent.transform.Find("Left_Move").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SelectCatagory((capturedIndex));
                    });
                    newContent.transform.Find("Left_Move").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SwapClothing((-1));
                    });

                    newContent.transform.Find("Rigth_Move").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SelectCatagory((capturedIndex));
                    });
                    newContent.transform.Find("Rigth_Move").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SwapClothing((1));
                    });

                    newContent.transform.Find("Customization_Menu").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SelectCatagory((capturedIndex));
                    });

                    newContent.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIcon;

                    GameObject newClothingRandomiser = Instantiate(ClothRandomiserPrefab, RandomiserContent);
                    randomiserClothingSection.Add(newClothingRandomiser);
                    newClothingRandomiser.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIcon;
                    newClothingRandomiser.transform.Find("name").GetComponent<Text>().text = GenderInformation[SelectedGender].ClothingOptions[i].CatagoryName.ToString();
                    newClothingRandomiser.transform.Find("Toggle").GetComponent<Toggle>().onValueChanged.AddListener(value => ClothingToggle(value, capturedIndex));
                }
            }

            GameObject MorphSectionDevider = Instantiate(SectionSettingPrefab, RandomiserContent);
            MorphSectionDevider.transform.Find("section").GetComponent<Text>().text = "Body Shape Section";
            MorphSectionDevider.GetComponent<Button>().onClick.AddListener(() => BodyMorphSectionState(SectionDevider.transform.Find("icon").GetComponent<Image>()));

            for(int x = 0; x < GenderInformation[SelectedGender].BlendShapes.Length; x++)
            {
                int capturedIndex = x;
                GameObject newClothingRandomiser = Instantiate(ClothRandomiserPrefab, RandomiserContent);
                randomiserMorphSection.Add(newClothingRandomiser);
                newClothingRandomiser.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[SelectedGender].BlendShapes[x].MuscleIcon;
                newClothingRandomiser.transform.Find("name").GetComponent<Text>().text = GenderInformation[SelectedGender].BlendShapes[x].DisplayShapeName;
                newClothingRandomiser.transform.Find("Toggle").GetComponent<Toggle>().onValueChanged.AddListener(value => BodyMorphToggle(value, capturedIndex));
            }

            for(int j = 0; j < GenderInformation[SelectedGender].BodyShape.Length; j++)
            {
                int capturedIndex = j;
                GameObject newBodyPart = Instantiate(ClothRandomiserPrefab, BodyPartsContent);
                newBodyPart.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[SelectedGender].BodyShape[j].Icon;
                newBodyPart.transform.Find("name").GetComponent<Text>().text = GenderInformation[SelectedGender].BodyShape[j].BodyPartName;
                newBodyPart.transform.Find("Toggle").GetComponent<Toggle>().onValueChanged.AddListener(value => BodyPartsToggle(value, capturedIndex));
                BodyPartsToggle(true, capturedIndex);
            }

            SelectCatagory(0);
            SwapCustomizeMenu(0);
        }
        /// <summary>
        /// this Function is called once at the start to create every base model panel and to select from 
        /// </summary>
        public void PrepareBaseModels()
        {
            foreach (Transform child in BaseModelContent) Destroy(child.gameObject);

            for (int i = 0; i < GenderInformation.Length; i++)
            {
                    GameObject newContent = Instantiate(BaseModelPrefab, BaseModelContent);
                    int capturedIndex = i;

                newContent.transform.Find("MeshName").GetComponent<Text>().text = GenderInformation[i].GenderName;
                newContent.transform.Find("MeshType").GetComponent<Text>().text = GenderInformation[i].meshType.ToString();

                    newContent.transform.Find("SwapMesh").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        ChangeGender((capturedIndex));
                    });

                    newContent.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[i].GenderPicture;
            }
        }
        /// <summary>
        /// this Function is called once at the start to create every Preset that are Saved in the Preset Json File
        /// </summary>
        public void PreparePresets()
        {
            Presets = LoadAllPresets();

            foreach (Transform child in PresetContent) Destroy(child.gameObject);

            if (Presets.Presets.Count <= 0) return;

            for (int i = 0; i < Presets.Presets.Count; i++)
            {
                GameObject newContent = Instantiate(PresetPrefab, PresetContent);
                int capturedIndex = i;

                newContent.transform.Find("PresetName").GetComponent<Text>().text = Presets.Presets[i].Description;
                newContent.GetComponent<Button>().onClick.AddListener(() =>
                {
                    LoadPreset((capturedIndex));
                });

                newContent.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[Presets.Presets[i].BaseMesh].GenderPicture;
            }

        }
        /// <summary>
        /// this Function is called once at the start to create every Preset that are Saved in the Preset Json File
        /// </summary>
        public void PrepareShapekeys()
        {
            foreach (Transform child in ShapeKeysContent) Destroy(child.gameObject);

            if (GenderInformation[SelectedGender].BlendShapes.Length <= 0) return;

            for (int i = 0; i < GenderInformation[SelectedGender].BlendShapes.Length; i++)
            {
                GameObject newContent = Instantiate(ShapeKeysPrefab, ShapeKeysContent);
                int capturedIndex = i;

                newContent.transform.Find("BlendShapeEntry").GetComponent<Text>().text = GenderInformation[SelectedGender].BlendShapes[i].DisplayShapeName;
                newContent.transform.Find("Value").GetComponent<Slider>().value = GenderInformation[SelectedGender].BlendShapes[i].weight;
                newContent.transform.Find("Value").GetComponent<Slider>().onValueChanged.AddListener((value) =>
                {
                    BlendShapeSLider(capturedIndex, value);
                });


                newContent.transform.Find("Icon").GetComponent<Image>().sprite = GenderInformation[SelectedGender].BlendShapes[i].MuscleIcon;
            }

        }
        #endregion

        #region Gender Selection Functions
        /// <summary>
        /// ChangeGender is used in Buttons that swap BaseModel such as (Young Female , Young Male ) also is called in other suitations 
        /// like when you start playing the scene and when u Undo/Redo the history function 
        /// </summary>
        public void ChangeGender(int index)
        {
            StopCoroutine(DisplayCharacter());
            SelectedGender = index;


            for (int i = 0; i < GenderInformation.Length; i++)
            {
                if (i != index) GenderInformation[i].GenderBaseMesh.SetActive(false);
                else GenderInformation[i].GenderBaseMesh.SetActive(true);
            }

            StartCoroutine(DisplayCharacter());
            if (SavingHistory) RecordNewChange();
        }
        /// <summary>
        /// Displays the selected basemesh in an animated way
        /// </summary>
        public IEnumerator DisplayCharacter()
        {
            float duration = 0.5f;
            float elapsed = 0f;

            Quaternion startRot = Quaternion.Euler(0, -100f, 0);
            Quaternion endRot = Quaternion.Euler(0, 0, 0);

            GenderInformation[SelectedGender].GenderBaseMesh.transform.rotation = startRot;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                GenderInformation[SelectedGender].GenderBaseMesh.transform.rotation = Quaternion.Lerp(startRot, endRot, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            GenderInformation[SelectedGender].GenderBaseMesh.transform.rotation = endRot;
        }


        #endregion

        #region Swap Clothing Functions
        /// <summary>
        /// The Arrow function to move from the list Left and Right </> to control which clothing is displayed through arrows .
        /// </summary>
        public void SwapClothing(int direction)
        {
            if (GenderInformation[SelectedGender].ClothingOptions.Length <= 0) return;

            MeshContainer currentGender = GenderInformation[SelectedGender];
            CatagoryDetail currentCategory = currentGender.ClothingOptions[SelectedCatagory];

            int maxIndex = currentCategory.ClothingObjects.Length - 1;
            int currentIndex = currentCategory.CatagoryIndex;

            int newIndex = currentIndex + direction;

            // Wrap newIndex between -1 and maxIndex (None = -1)
            if (newIndex > maxIndex)
                newIndex = -1;
            else if (newIndex < -1)
                newIndex = maxIndex;

            // Update SpawnedButtons colors
            // Reset previous selected button color
            if (currentIndex == -1)
                SpawnedButtons[0].color = NormalClothIndex;  // None button is index 0
            else
                SpawnedButtons[currentIndex + 1].color = NormalClothIndex;

            // Set new selected button color
            if (newIndex == -1)
                SpawnedButtons[0].color = SelectedClothIndex;
            else
                SpawnedButtons[newIndex + 1].color = SelectedClothIndex;

            // Activate clothing models
            for (int i = 0; i < currentCategory.ClothingObjects.Length; i++)
            {
                currentCategory.ClothingObjects[i].model.SetActive(i == newIndex);
            }

            currentCategory.CatagoryIndex = newIndex;

            // Update UI text and icons
            ClosetTitle.text = currentCategory.CatagoryName.ToString();
            ClosetIcon.sprite = currentCategory.CatagoryIcon;

            if (newIndex == -1)
            {
                ModelName[SelectedCatagory].text = "None";
                ClothAmount[SelectedCatagory].text = "0/" + currentCategory.ClothingObjects.Length.ToString();
            }
            else
            {
                ModelName[SelectedCatagory].text = currentCategory.ClothingObjects[newIndex].model.name;
                ClothAmount[SelectedCatagory].text = (newIndex + 1).ToString() + "/" + currentCategory.ClothingObjects.Length.ToString();
            }

            if (currentCategory.CatagoryName == CatagoryDetail.CatagoryNames.Hats)
            {
                int hatIndex = currentCategory.CatagoryIndex;

                if (hatIndex >= 0)
                {
                    var hat = currentCategory.ClothingObjects[hatIndex];
                    ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                }
                else
                {
                    ApplyHatShapeKey(0f); // No hat remove hair deformation
                }
            }
            else if (currentCategory.CatagoryName == CatagoryDetail.CatagoryNames.HairStyle)
            {
                for (int j = 0; j < GenderInformation[SelectedGender].ClothingOptions.Length; j++)
                {
                    if (GenderInformation[SelectedGender].ClothingOptions[j].CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                    {
                        int hatIndex = GenderInformation[SelectedGender].ClothingOptions[j].CatagoryIndex;

                        if (hatIndex >= 0)
                        {
                            var hat = GenderInformation[SelectedGender].ClothingOptions[j].ClothingObjects[hatIndex];
                            ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                        }
                        else
                        {
                            ApplyHatShapeKey(0f); // No hat remove hair deformation
                        }
                    }
                }
            }

                HeadColors();
            if (SavingHistory) RecordNewChange();
        }
        /// <summary>
        /// Selects which catagory are we displaying to adjust this function is added into buttons to control via UI , its used in another case such as
        /// history undo/redo .
        /// </summary>
        public void SelectCatagory(int Catagory)
        {
            SelectedCatagory = Catagory;
            ArrangeCloset();
            HeadColors();
        }
        /// <summary>
        /// Displays all the UI Needed for the Selected Catagory .
        /// </summary>
        public void ArrangeCloset()
        {
            MeshContainer currentGender = GenderInformation[SelectedGender];
            CatagoryDetail currentCategory = currentGender.ClothingOptions[SelectedCatagory];

            // Set Closet Title and Icon
            ClosetTitle.text = currentCategory.CatagoryName.ToString();
            ClosetIcon.sprite = currentCategory.CatagoryIcon; // new variable you added in CatagoryDetail

            // Clear previous buttons
            foreach (Transform child in ClosetContent)
            {
                Destroy(child.gameObject);
            }
            SpawnedButtons.Clear();

            #region Add NoneButton
            GameObject NoneButton = Instantiate(ItemHolderUI, ClosetContent);
            Image NoneImage = NoneButton.transform.Find("img").GetComponent<Image>();
            NoneButton.transform.Find("N.").GetComponent<Text>().text = "0";
            NoneImage.sprite = NoneIcon;

            SpawnedButtons.Add(NoneImage);

            NoneButton.GetComponent<Button>().onClick.AddListener(() => SelectCloth(-1));
            SpawnedButtons[0].color = (currentCategory.CatagoryIndex == - 1) ? SelectedClothIndex : NormalClothIndex;
            #endregion

            if (currentCategory.ClothingObjects.Length <= 0) return;
            // Spawn new buttons for each clothing object in this category
            for (int i = 0; i < currentCategory.ClothingObjects.Length; i++)
            {
                GameObject newButton = Instantiate(ItemHolderUI, ClosetContent);
                Image buttonImage = newButton.transform.Find("img").GetComponent<Image>();
                newButton.transform.Find("N.").GetComponent<Text>().text = (i + 1).ToString();
                buttonImage.sprite = currentCategory.ClothingObjects[i].Icon;

                SpawnedButtons.Add(buttonImage);

                int index = i;
                newButton.GetComponent<Button>().onClick.AddListener(() => SelectCloth(index));
                SpawnedButtons[i + 1].color = (currentCategory.CatagoryIndex == i) ? SelectedClothIndex : NormalClothIndex;
            }
            ClosetUI.SetActive(true);
        }
        /// <summary>
        /// this Function is added inside buttons that are displayed in the cloth closet to select certain clothing index
        /// </summary>
        public void SelectCloth(int clothIndex)
        {
            MeshContainer currentGender = GenderInformation[SelectedGender];
            CatagoryDetail currentCategory = currentGender.ClothingOptions[SelectedCatagory];

            if (clothIndex == -1)
            {
                // None selected: deactivate all clothing objects
                for (int i = 0; i < currentCategory.ClothingObjects.Length; i++)
                {
                    currentCategory.ClothingObjects[i].model.SetActive(false);
                    SpawnedButtons[i + 1].color = NormalClothIndex;  // buttons after None button
                }
                // Set the None button color to selected
                SpawnedButtons[0].color = SelectedClothIndex;

                // Update the CatagoryIndex to -1 to represent no clothing
                currentCategory.CatagoryIndex = -1;

                // Optionally reset material indices for all clothes or do nothing
            }
            else
            {
                // A clothing piece is selected
                for (int i = 0; i < currentCategory.ClothingObjects.Length; i++)
                {
                    bool isSelected = (i == clothIndex);
                    currentCategory.ClothingObjects[i].model.SetActive(isSelected);
                    SpawnedButtons[i + 1].color = isSelected ? SelectedClothIndex : NormalClothIndex;
                }
                // None button color reset
                SpawnedButtons[0].color = NormalClothIndex;

                // Update CatagoryIndex to selected index
                currentCategory.CatagoryIndex = clothIndex;

                // Update material index array inside the selected clothing object
                currentCategory.ClothingObjects[clothIndex].CurrentIndex = new int[currentCategory.ClothingObjects[clothIndex].materialSlotNames.Length];
            }

            // Update UI text and amount display after selection
            ClosetTitle.text = currentCategory.CatagoryName.ToString();
            ClosetIcon.sprite = currentCategory.CatagoryIcon;

            if (clothIndex == -1)
            {
                ModelName[SelectedCatagory].text = "None";
                ClothAmount[SelectedCatagory].text = $"0/{currentCategory.ClothingObjects.Length}";
            }
            else
            {
                ModelName[SelectedCatagory].text = currentCategory.ClothingObjects[clothIndex].model.name;
                ClothAmount[SelectedCatagory].text = $"{clothIndex + 1}/{currentCategory.ClothingObjects.Length}";
            }

            if (currentCategory.CatagoryName == CatagoryDetail.CatagoryNames.Hats)
            {
                int hatIndex = currentCategory.CatagoryIndex;

                if (hatIndex >= 0)
                {
                    var hat = currentCategory.ClothingObjects[hatIndex];
                    ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                }
                else
                {
                    ApplyHatShapeKey(0f); // No hat remove hair deformation
                }
            }
            else if (currentCategory.CatagoryName == CatagoryDetail.CatagoryNames.HairStyle)
            {
                for (int j = 0; j < GenderInformation[SelectedGender].ClothingOptions.Length; j++)
                {
                    if (GenderInformation[SelectedGender].ClothingOptions[j].CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                    {
                        int hatIndex = GenderInformation[SelectedGender].ClothingOptions[j].CatagoryIndex;

                        if (hatIndex >= 0)
                        {
                            var hat = GenderInformation[SelectedGender].ClothingOptions[j].ClothingObjects[hatIndex];
                            ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                        }
                        else
                        {
                            ApplyHatShapeKey(0f); // No hat remove hair deformation
                        }
                    }
                }
            }

            HeadColors();
            if (SavingHistory) RecordNewChange();
        }
        #endregion

        #region MultiColoring Functions
        /// <summary>
        /// displayes the color closet menu , also displayes how many head colors are displayed
        /// </summary>
        private void HeadColors()
        {
            var currentClothing = GetCurrentSelectedClothing();
            if(!ColorClosetUI.activeSelf) ColorClosetUI.SetActive(true);
            for (int i = 0; i < HeadColorsBut.Length; i++)
            {
                if (currentClothing != null)
                {
                    HeadColorsBut[i].SetActive(i < currentClothing.materialSlotNames.Length);
                }
                else
                {
                    HeadColorsBut[i].SetActive(false);

                }
            }

            if (currentClothing != null)
            {
                // Always show the first material slot when opening colors
                OnMaterialSlotButtonClicked(0);
            }
            else
            {
                foreach (Transform child in ColorContent)
                    Destroy(child.gameObject);
            }
        }
        /// <summary>
        /// this function is added to the head colors buttons to select which material slot are we controlling .
        /// </summary>
        public void OnMaterialSlotButtonClicked(int slotIndex)
        {
            SelectedMaterialSlot = slotIndex;
            ArrangeMaterial(slotIndex);
        }
        /// <summary>
        /// this is a helper function called to detect which cloth are we selecting currently
        /// </summary>
        private CustomizableObject GetCurrentSelectedClothing()
        {
            var currentCategory = GenderInformation[SelectedGender].ClothingOptions[SelectedCatagory];
            int selectedClothIndex = currentCategory.CatagoryIndex;

            if (selectedClothIndex < 0 || selectedClothIndex >= currentCategory.ClothingObjects.Length)
                return null;

            return currentCategory.ClothingObjects[selectedClothIndex];
        }
        /// <summary>
        /// arrange Material is used to display all the buttons for every color we have aka materials .
        /// </summary>
        public void ArrangeMaterial(int materialSlotIndex)
        {
            var currentClothing = GetCurrentSelectedClothing();

            foreach (Transform child in ColorContent)
                Destroy(child.gameObject);

            if (currentClothing == null || currentClothing.model == null)
            {
                Debug.LogWarning("No clothing or model selected for coloring.");
                return;
            }

            SelectedMaterialSlot = materialSlotIndex;

            var matType = currentClothing.materialSlotNames[materialSlotIndex];
            List<Material> materialsToUse = null;

            switch (matType)
            {
                case CustomizableObject.MaterialType.Normal:
                    materialsToUse = ColorMaterials.ToList();
                    break;
                case CustomizableObject.MaterialType.SeeThrough:
                    materialsToUse = GlassMaterial.ToList();
                    break;
                default:
                    Debug.LogWarning("Unsupported material type for coloring");
                    return;
            }

            for (int i = 0; i < materialsToUse.Count; i++)
            {
                GameObject newHolder = Instantiate(ColorKnob, ColorContent);
                Image colorImage = newHolder.transform.Find("Color").GetComponent<Image>();
                colorImage.color = materialsToUse[i].color;

                int capturedIndex = i;
                newHolder.GetComponent<Button>().onClick.AddListener(() => ApplyColor(capturedIndex));
            }

            // After creating the knobs, highlight the current color index for this slot
            UpdateColorButtonVisuals(currentClothing.CurrentIndex[materialSlotIndex]);
        }
        /// <summary>
        /// this is the function added to the coloring button to change the materials of the clothing objects
        /// </summary>
        private void ApplyColor(int colorIndex, bool updateUI = true)
        {
            var currentClothing = GetCurrentSelectedClothing();
            if (currentClothing == null || currentClothing.model == null) return;

            SkinnedMeshRenderer smr = currentClothing.model.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) return;

            Material[] mats = smr.sharedMaterials;

            int slot = SelectedMaterialSlot;
            if (slot < 0 || slot >= mats.Length) return;

            currentClothing.CurrentIndex[slot] = colorIndex;

            if (currentClothing.materialSlotNames[slot] == CustomizableObject.MaterialType.Normal)
            {
                mats[slot] = ColorMaterials[colorIndex];
            }
            else
            {
                mats[slot] = GlassMaterial[colorIndex];
            }

            smr.sharedMaterials = mats;

            if (updateUI) UpdateColorButtonVisuals(colorIndex);

            if (SavingHistory) RecordNewChange();
        }
        /// <summary>
        /// this function basicly changes the colors of the buttons is visuals to display the same colors as the materials inside of Color Material Array .
        /// </summary>
        private void UpdateColorButtonVisuals(int selectedColorIndex)
        {
            for (int i = 0; i < ColorContent.childCount; i++)
            {
                var border = ColorContent.GetChild(i).GetComponent<Image>();

                // You need a visual indicator separate from the actual color image
                // I assume here you have a background image on the knob holder that we can tint for selection
                if (border != null)
                {
                    border.color = (i == selectedColorIndex) ? SelectedHeadColor : NormalHeadColor;
                }
            }
        }
        #endregion

        #region History Functions
        /// <summary>
        /// this function is used to check at which spot are we currently of the history array and removes any thing infront of the current index then add
        /// a new record after the currently record we are at .
        /// </summary>
        public void RecordNewChange()
        {
            SavingHistory = false;
            if (CurrentHistoryIndex < ChangeHistory.Count - 1)
            {
                ChangeHistory.RemoveRange(CurrentHistoryIndex + 1, ChangeHistory.Count - (CurrentHistoryIndex + 1));
            }

            SaveHistory();

            CurrentHistoryIndex = ChangeHistory.Count - 1;
            SavingHistory = true;
        }
        /// <summary>
        /// move back in the list of history if its not at 0
        /// </summary>
        public void Undo()
        {
            if (CurrentHistoryIndex > 0)
            {
                CurrentHistoryIndex--;
                ApplyHistory();
            }
        }
        /// <summary>
        /// moves forward in the list of history if its not at the end of the list .
        /// </summary>
        public void Redo()
        {
            if (CurrentHistoryIndex < ChangeHistory.Count - 1)
            {
                CurrentHistoryIndex++;
                ApplyHistory();
            }
        }
        /// <summary>
        /// this function is what changes the appearance and values of the currently displayed character when we move left or right of the changes history.
        /// </summary>
        public void ApplyHistory()
        {
            if (ChangeHistory.Count == 0 || CurrentHistoryIndex < 0 || CurrentHistoryIndex >= ChangeHistory.Count)
            {
                Debug.LogWarning("No history to apply or index out of range.");
                return;
            }

            SavingHistory = false;

            History history = ChangeHistory[CurrentHistoryIndex];

            if (history.BaseMesh != SelectedGender)
            {
                ChangeGender(history.BaseMesh);
                PrepareAppearance();
            }

            if (history.ToneSkin != GenderInformation[SelectedGender].currentSkinColor)
                ChangeSkinColor(history.ToneSkin);

            var clothingCategories = GenderInformation[SelectedGender].ClothingOptions;

            for (int i = 0; i < history.clothHistory.Count; i++)
            {
                if (i >= clothingCategories.Length) break;  // safety check

                SelectCatagory(i);
                int clothIndex = history.clothHistory[i].ClothingIndex;

                // Apply the clothing selection
                SelectCloth(clothIndex);

                if (clothIndex < 0) continue;  // no material changes if no clothing selected

                var currentClothing = clothingCategories[SelectedCatagory].ClothingObjects[clothIndex];

                // Ensure we don't go out of range even if history has fewer materials stored than the current slots
                int materialSlots = currentClothing.CurrentIndex.Length;
                int savedMaterials = history.clothHistory[i].MaterialIndex.Count;

                for (int x = 0; x < materialSlots; x++)
                {
                    SelectedMaterialSlot = x;

                    int materialIndex = 0;
                    if (x < savedMaterials)
                    {
                        materialIndex = history.clothHistory[i].MaterialIndex[x];
                    }

                    ApplyColor(materialIndex);
                }
            }

            for(int x = 0; x < history.ShapeKeys.Count;x++)
            {
                BlendShapeSLider(x, history.ShapeKeys[x]);
            }

            for (int j = 0; j < GenderInformation[SelectedGender].ClothingOptions.Length; j++)
            {
                if (GenderInformation[SelectedGender].ClothingOptions[j].CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                {
                    int hatIndex = GenderInformation[SelectedGender].ClothingOptions[j].CatagoryIndex;

                    if (hatIndex >= 0)
                    {
                        var hat = GenderInformation[SelectedGender].ClothingOptions[j].ClothingObjects[hatIndex];
                        ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                    }
                    else
                    {
                        ApplyHatShapeKey(0f); // No hat remove hair deformation
                    }
                }
            }
            // Reset UI after applying
            SelectCatagory(0);
            HeadColors();
            SavingHistory = true;
        }
        /// <summary>
        /// Saving the changes that happens for every detail of the character , every thing is recorded except moving through menus .
        /// </summary>
        public void SaveHistory()
        {
            History newHistory = new History();
            newHistory.BaseMesh = SelectedGender ;
            newHistory.ToneSkin = GenderInformation[SelectedGender].currentSkinColor;

            newHistory.clothHistory = new List<ClothHistory>();
            newHistory.ShapeKeys = new List<float>();
            foreach (CatagoryDetail cloth in GenderInformation[SelectedGender].ClothingOptions)
            {
               ClothHistory newCloth = new ClothHistory();
                newCloth.ClothingIndex = cloth.CatagoryIndex;
                newCloth.MaterialIndex = new List<int>();
                if (cloth.CatagoryIndex >= 0)
                {
                    foreach (int i in cloth.ClothingObjects[cloth.CatagoryIndex].CurrentIndex)
                    {
                        newCloth.MaterialIndex.Add(i);
                    }
                }
                newHistory.clothHistory.Add(newCloth);
            }

            foreach (BlendShapeEntry shapekey in GenderInformation[SelectedGender].BlendShapes)
            {
                newHistory.ShapeKeys.Add(shapekey.weight);
            }
            ChangeHistory.Add(newHistory);
        }

        #endregion

        #region Preset Functions
        /// <summary>
        /// Creates a json file containing all the presets 
        /// or loads all the presets from the json file if it exists and writes over it .
        /// </summary>
        public void SavePresetJson(string PresetName)
        {
#if UNITY_EDITOR
            // Create new preset (same logic as before)
            History history = new History
            {
                BaseMesh = SelectedGender,
                ToneSkin = GenderInformation[SelectedGender].currentSkinColor,
                Description = PresetName,
                clothHistory = new List<ClothHistory>(),
                ShapeKeys = new List<float>()
            };

            foreach (CatagoryDetail cloth in GenderInformation[SelectedGender].ClothingOptions)
            {
                ClothHistory clothinghistory = new ClothHistory();
                clothinghistory.ClothingIndex = cloth.CatagoryIndex;
                clothinghistory.MaterialIndex = new List<int>();

                if (cloth.CatagoryIndex >= 0)
                {
                    CustomizableObject activeClothing = cloth.ClothingObjects[cloth.CatagoryIndex];
                    clothinghistory.MaterialIndex = new List<int>(activeClothing.CurrentIndex);
                }

                history.clothHistory.Add(clothinghistory);
            }
            foreach (BlendShapeEntry shapekey in GenderInformation[SelectedGender].BlendShapes)
            {
                history.ShapeKeys.Add(shapekey.weight);
            }
            // Load existing preset collection (if it exists)
            string folderPath = "Assets/CharCrafter - Moduler Low-Poly Character Creation/JsonFiles";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, "PresetCollection.json");
            PresetCollection presetCollection = new PresetCollection();
            presetCollection.Presets = new List<History>();
            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                presetCollection = JsonUtility.FromJson<PresetCollection>(existingJson);
            }

            // Add new preset
            presetCollection.Presets.Add(history);

            // Save updated collection
            string updatedJson = JsonUtility.ToJson(presetCollection, true);
            File.WriteAllText(filePath, updatedJson);
            AssetDatabase.Refresh();

            Debug.Log("Preset saved to: " + filePath);
            PreparePresets();
#endif
        }
        /// <summary>
        /// Load the presets its a helping method called inside preparePresets to load all the data from the json file into PresetCollection
        /// </summary>
        public PresetCollection LoadAllPresets()
        {
            string filePath = "Assets/CharCrafter - Moduler Low-Poly Character Creation/JsonFiles/PresetCollection.json";

            if (!File.Exists(filePath))
            {
                Debug.LogWarning("No presets found.");
                return new PresetCollection();
            }

            string json = File.ReadAllText(filePath);
            PresetCollection presetCollection = JsonUtility.FromJson<PresetCollection>(json);
            return presetCollection;
        }
        /// <summary>
        /// The function that is used inside the buttons to load the presets in the preset menu
        /// </summary>
        public void LoadPreset(int index)
        {
            if (Presets.Presets.Count <= 0 || index > Presets.Presets.Count)
            {
                Debug.LogWarning("no presets or the preset index doesnt exist");
                return;
            }

            SavingHistory = false;

            History history = Presets.Presets[index];

            if (history.BaseMesh != SelectedGender)
            {
                ChangeGender(history.BaseMesh);
                PrepareAppearance();
            }

            if (history.ToneSkin != GenderInformation[SelectedGender].currentSkinColor)
                ChangeSkinColor(history.ToneSkin);

            var clothingCategories = GenderInformation[SelectedGender].ClothingOptions;

            for (int i = 0; i < history.clothHistory.Count; i++)
            {
                if (i >= clothingCategories.Length) break;  // safety check

                SelectCatagory(i);
                int clothIndex = history.clothHistory[i].ClothingIndex;

                // Apply the clothing selection
                SelectCloth(clothIndex);

                if (clothIndex < 0) continue;  // no material changes if no clothing selected

                var currentClothing = clothingCategories[SelectedCatagory].ClothingObjects[clothIndex];

                // Ensure we don't go out of range even if history has fewer materials stored than the current slots
                int materialSlots = currentClothing.CurrentIndex.Length;
                int savedMaterials = history.clothHistory[i].MaterialIndex.Count;

                for (int x = 0; x < materialSlots; x++)
                {
                    SelectedMaterialSlot = x;

                    int materialIndex = 0;
                    if (x < savedMaterials)
                    {
                        materialIndex = history.clothHistory[i].MaterialIndex[x];
                    }

                    ApplyColor(materialIndex);
                }
            }

            for (int x = 0; x < history.ShapeKeys.Count; x++)
            {
                BlendShapeSLider(x, history.ShapeKeys[x]);
            }

            for (int j = 0; j < GenderInformation[SelectedGender].ClothingOptions.Length; j++)
            {
                if (GenderInformation[SelectedGender].ClothingOptions[j].CatagoryName == CatagoryDetail.CatagoryNames.Hats)
                {
                    int hatIndex = GenderInformation[SelectedGender].ClothingOptions[j].CatagoryIndex;

                    if (hatIndex >= 0)
                    {
                        var hat = GenderInformation[SelectedGender].ClothingOptions[j].ClothingObjects[hatIndex];
                        ApplyHatShapeKey(hat.HairFix ? 100f : 0f);
                    }
                    else
                    {
                        ApplyHatShapeKey(0f); // No hat remove hair deformation
                    }
                }
            }
            // Reset UI after applying
            SelectCatagory(0);
            HeadColors();
            SavingHistory = true;
        }

        public void DisplaySavePresetMenu(bool value)
        {
            SavePresetMenu.SetActive(value);
        }
        public void SavePresetName()
        {
            SavePresetJson(SavePresetTxt.text);
        }
        #endregion

        #region BlendShapeKeys Functions
        /// <summary>
        /// Adjusts the paramters values of the character shape , while loading and reloading the slider is displayed value .
        /// </summary>
        public void BlendShapeSLider(int index, float value)
        {
            GenderInformation[SelectedGender].BlendShapes[index].weight = value;

            string shapeName = GenderInformation[SelectedGender].BlendShapes[index].OriginalShapeName;

            int getbaseindex = GenderInformation[SelectedGender].GenderBaseRenderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (getbaseindex >= 0)
            {
                GenderInformation[SelectedGender].GenderBaseRenderer.SetBlendShapeWeight(getbaseindex, value);
            }
            for (int i = 0; i < GenderInformation[SelectedGender].BlendShapes[index].SyncedClothing.Length; i++)
            {
                var clothingRenderer = GenderInformation[SelectedGender].BlendShapes[index].SyncedClothing[i];
                int shapekey = clothingRenderer.sharedMesh.GetBlendShapeIndex(shapeName);

                if (shapekey >= 0)
                {
                    clothingRenderer.SetBlendShapeWeight(shapekey, value);
                }
            }
        }
        /// <summary>
        /// a function solely made to control the custom hat compatability that adjusts by it self weather we have a hat or not .
        /// </summary>
        public void ApplyHatShapeKey(float value)
        {
            for(int i = 0; i < GenderInformation[SelectedGender].ClothingOptions.Length;i++)
            {
                if (GenderInformation[SelectedGender].ClothingOptions[i].CatagoryName == CatagoryDetail.CatagoryNames.HairStyle)
                {
                    if (GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIndex >= 0)
                    {
                        GenderInformation[SelectedGender].ClothingOptions[i].ClothingObjects[GenderInformation[SelectedGender].ClothingOptions[i].CatagoryIndex].model.GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, value);
                        return;
                    }
                    else return;
                }
            }
        }

        #endregion

        #region Body Parts Functions

        public void BodyPartsToggle(bool value , int index)
        {
            GenderInformation[SelectedGender].BodyShape[index].Toggle = value;
            GenderInformation[SelectedGender].BodyShape[index].BodyPart.SetActive(value);
        }

        #endregion

        #region Settings
        /// <summary>
        /// a simple function that opens my discord page , if you need help or want ask for a future in the asset here is how to contact me.
        /// </summary>
        public void JoinDiscordCommunity()
        {
            Application.OpenURL("https://discord.gg/AqzJBWEqbz");
        }

        #endregion

    }
    [System.Serializable]
    public class PresetCollection
    {
        public List<History> Presets = new List<History>();
    }
    /// <summary>
    /// the class that contains all the data of history
    /// </summary>
    [System.Serializable]
    public class History
    {
        public int BaseMesh;
        public int ToneSkin;
        public string Description; // used for presets
        public List<ClothHistory> clothHistory;
        public List<float> ShapeKeys;
    }
    /// <summary>
    /// the class that contains all the subdata of history such as clothes indexes
    /// </summary>
    [System.Serializable]
    public class ClothHistory
    {
        public int ClothingIndex;
        public List<int> MaterialIndex;
    }

    public class RandomserControlParamater
    {
        public List<bool> Clothing;
        public List<bool> BodyMorph;
    }
}