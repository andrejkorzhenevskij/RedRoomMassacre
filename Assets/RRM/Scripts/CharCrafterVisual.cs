using UnityEngine;

namespace RRM
{
    // Visual-only adapter for CharCrafter actors. RRM still owns items and combat.
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class CharCrafterVisual : MonoBehaviour
    {
        public Animator animator;
        public Transform leftHand;
        public Transform rightHand;
        public Vector3 cameraOffset = new Vector3(0f, 0.04f, 0.12f);
        public Vector3 lampOffset = new Vector3(0f, -0.66f, 0f);

        private PlayerController controller;
        private MeleeAttack attack;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            attack = GetComponent<MeleeAttack>();
        }

        private void LateUpdate()
        {
            if (!controller || !controller.isActiveAndEnabled || GetComponent<Damageable>().IsDead) return;
            FollowHand(controller.LeftHand, leftHand);
            FollowHand(controller.RightHand, rightHand);
        }

        private void FollowHand(PlayerHand hand, Transform bone)
        {
            Component item = hand.Item?.Source;
            if (!item || !bone || !hand.IsAvailable || item is TwoHandedAxe || (attack && attack.IsBusy)) return;
            // Keep the device parent/rotation relative to its gameplay owner, including independently aimed cameras.
            Vector3 offset = item is CameraDevice ? cameraOffset : lampOffset;
            item.transform.position = bone.position + transform.TransformDirection(offset);
        }

        internal void PoseAxe(TwoHandedAxe axe)
        {
            if (!leftHand || !rightHand || !axe || !attack) return;
            if (attack.IsUsingAxe(axe))
            {
                axe.transform.SetPositionAndRotation(attack.weaponPivot.position, attack.weaponPivot.rotation);
                return;
            }
            // ponytail: fixed bone pose and an approximate grip span; animation/IK is outside this visual experiment.
            Vector3 direction = leftHand.position - rightHand.position;
            if (direction.sqrMagnitude < 0.001f) return;
            axe.transform.rotation = Quaternion.LookRotation(direction, transform.up);
            axe.transform.position = rightHand.position - axe.transform.forward * 0.22f;
        }
    }
}
