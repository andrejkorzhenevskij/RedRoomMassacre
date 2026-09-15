namespace AyuoDev.CharCrafter
{
    using UnityEngine;
    /// <summary>
    /// Base Mesh data is all recorded here inside of this class
    /// </summary>
    public class MeshContainer : MonoBehaviour
    {
        public string GenderName;
        public GameObject GenderBaseMesh;
        public SkinnedMeshRenderer GenderBaseRenderer;
        public MeshType meshType;
        public int currentSkinColor;
        public Sprite GenderPicture;

        public CatagoryDetail[] ClothingOptions;
        public BodyShapes[] BodyShape;
        public BlendShapeEntry[] BlendShapes;
        public enum MeshType
        {
            Humen = 0
        }
    }
    /// <summary>
    /// this is where we save the data of each clothing object
    /// </summary>
    [System.Serializable]
    public class CustomizableObject
    {
        public enum MaterialType
        {
            Normal = 0,
            SeeThrough = 1
        }
        public GameObject model;
        public Sprite Icon;
        public MaterialType[] materialSlotNames;
        public int[] CurrentIndex;

        // Hat Specific Variable
        public bool HairFix;
    }
    /// <summary>
    /// this is where we save the data of the clothing catagory such as ( top clothes , bottom clothes , etc )
    /// </summary>
    [System.Serializable]
    public class CatagoryDetail
    {
        public CatagoryNames CatagoryName;
        public Sprite CatagoryIcon;
        public CustomizableObject[] ClothingObjects;
        public int CatagoryIndex;

        public enum CatagoryNames
        {
            TopClothes,
            BottomClothes,
            Jackets,
            Shoes,
            HairStyle,
            FacialHair,
            Gloves,
            Hats,
            Glasses,
            BackBag,
            EyeBrows
        }
    }
    [System.Serializable]
    public class BlendShapeEntry
    {
        public string DisplayShapeName;
        public string OriginalShapeName;
        public Sprite MuscleIcon;
        [Range(0, 100)] public float weight;

        public SkinnedMeshRenderer[] SyncedClothing;
    }
    [System.Serializable]
    public class BodyShapes
    {
        public string BodyPartName;
        public Sprite Icon;
        public GameObject BodyPart;
        public bool Toggle;
    }
}