using UnityEngine;
using VRC.SDK3.Avatars.Components;

#nullable enable

namespace world.anlabo.mdnailtool.Runtime.Components { 
    
    [AddComponentMenu("An-Labo/Nail Tool/MDNailAutoInstaller")] 
    public class MDNailAutoInstaller : MonoBehaviour {

        [Header("--- Target Avatar ---")]
        [Tooltip("[MDNail Installer]")]
        public VRCAvatarDescriptor? targetAvatar;

        [Header("--- Avatar Info ---")]
        public string shopName = "";
        public string avatarName = "";
        public string variationName = "";

        [Header("--- Settings ---")]
        public string designName = ""; 
        public string materialName = "";
        public string colorName = "";
        public string nailShape = "Oval"; 

        [Header("--- Options ---")]
        public bool useFootNail = false;
        
        [HideInInspector] public bool useModularAvatar = true; 
    }
}