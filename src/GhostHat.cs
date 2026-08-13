using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace GhostHats
{
    /// <summary>
    /// Clones the ghost owner's hat onto their PlayerGhost.
    ///
    /// Placement is measured, not hardcoded: both the character and the ghost have the same
    /// face pieces (two eye renderers and a mouth renderer), which define a face frame with
    /// its origin at the eye midpoint, right along the eye line, up towards the eyes from the
    /// mouth, forward from their cross product. The hat's transform is expressed in the
    /// character's face frame and re-applied in the ghost's, scaled by the ratio of the two
    /// eye spacings. That survives model/prefab changes and needs no magic numbers.
    ///
    /// The component itself carries no state; it exists so a ghost can't be hatted twice.
    /// </summary>
    internal class GhostHat : MonoBehaviour
    {
        /// <summary>Builds the hat for a freshly initialised ghost. Safe to call more than once.</summary>
        internal static void Attach(PlayerGhost ghost)
        {
            if (ghost == null || ghost.m_owner == null) return;
            if (ghost.GetComponent<GhostHat>() != null) return;

            Character owner = ghost.m_owner;

            // Vanilla hides your own ghost from you (the spectator camera sits inside it), so
            // a hat here would just be a hat in your face.
            PhotonView ownerView = owner.photonView;
            if (ownerView != null && ownerView.IsMine) return;

            ghost.gameObject.AddComponent<GhostHat>().Build(ghost, owner);
        }

        private void Build(PlayerGhost ghost, Character owner)
        {
            Renderer source = ResolveHatRenderer(owner);
            if (source == null)
            {
                Destroy(this);
                return;
            }

            CustomizationRefs charRefs = owner.refs.customization.refs;
            if (!TryFaceFrame(charRefs.EyeRenderers, charRefs.mouthRenderer, out Vector3 charCenter, out Quaternion charRot, out float charSpan) ||
                !TryFaceFrame(ghost.EyeRenderers, ghost.mouthRenderer, out Vector3 ghostCenter, out Quaternion ghostRot, out float ghostSpan))
            {
                Plugin.Log.LogWarning("Could not measure a face (two eyes + a mouth) on the character or the " +
                                      "ghost, so there is nothing to line the hat up with. Did the game update?");
                Destroy(this);
                return;
            }

            GameObject clone = Instantiate(source.gameObject);
            clone.name = "GhostHat";
            clone.SetActive(true);
            foreach (Collider c in clone.GetComponentsInChildren<Collider>(true)) Destroy(c);
            SetLayerRecursively(clone.transform, GhostLayer(ghost));

            // Similarity transform: character face frame -> ghost face frame.
            Quaternion frameDelta = ghostRot * Quaternion.Inverse(charRot);
            float k = ghostSpan / charSpan;

            Transform src = source.transform;
            Vector3 worldPos = ghostCenter + frameDelta * (src.position - charCenter) * k;
            Quaternion worldRot = frameDelta * src.rotation;
            Vector3 worldScale = src.lossyScale * k;

            Transform g = ghost.transform;
            Vector3 gs = g.lossyScale;

            Transform hat = clone.transform;
            hat.SetParent(g, false);
            hat.localPosition = g.InverseTransformPoint(worldPos);
            hat.localRotation = Quaternion.Inverse(g.rotation) * worldRot;
            hat.localScale = new Vector3(
                worldScale.x / NonZero(gs.x),
                worldScale.y / NonZero(gs.y),
                worldScale.z / NonZero(gs.z));

            Log($"Hat '{source.name}' on {owner.characterName}'s ghost: scale ratio {k:F3}, " +
                $"local pos {hat.localPosition}.");
        }

        /// <summary>The hat the owner is actually wearing, mirroring CharacterCustomization.OnPlayerDataChange.</summary>
        private static Renderer ResolveHatRenderer(Character owner)
        {
            CharacterCustomization customization = owner.refs != null ? owner.refs.customization : null;
            if (customization == null || customization.refs == null || customization.refs.playerHats == null)
            {
                Log($"{owner.characterName} has no customization refs, so no hat.");
                return null;
            }

            Renderer[] hats = customization.refs.playerHats;
            PersistentPlayerData data = GameHandler.GetService<PersistentPlayerDataService>().GetPlayerData(owner.photonView.Owner);
            if (data == null) return null;

            int index = data.customizationData.currentHat;

            // Some outfits force their own hat.
            Customization c = Singleton<Customization>.Instance;
            if (c != null)
            {
                CustomizationOption fit = c.fits[CharacterCustomization.GetFitIndex(data)];
                if (fit != null && fit.overrideHat) index = fit.overrideHatIndex;

                if (index >= 0 && index < c.hats.Length && c.hats[index] != null && c.hats[index].isBlank)
                {
                    Log($"{owner.characterName} is not wearing a hat.");
                    return null;
                }
            }

            if (index < 0 || index >= hats.Length)
            {
                Log($"Hat index {index} is out of range (0..{hats.Length - 1}), so no hat.");
                return null;
            }

            Renderer hat = hats[index];
            if (hat == null || !HasVisibleMesh(hat))
            {
                Log($"Hat index {index} has no mesh, so no hat.");
                return null;
            }

            return hat;
        }

        /// <summary>
        /// Face frame from the eyes and mouth: origin at the eye midpoint, +X along the eye
        /// line, +Y from the mouth towards the eyes, +Z their cross product. Both models use
        /// the same renderer ordering, so even if left/right are swapped the mapping between
        /// the two frames stays consistent.
        /// </summary>
        private static bool TryFaceFrame(Renderer[] eyes, Renderer mouth, out Vector3 center, out Quaternion rotation, out float span)
        {
            center = Vector3.zero;
            rotation = Quaternion.identity;
            span = 0f;

            if (eyes == null || eyes.Length < 2 || mouth == null) return false;

            // The widest-apart pair is the left and right eye; both models may carry extra
            // renderers (shadows, overlays) sitting on top of one of them.
            Vector3 a = Vector3.zero, b = Vector3.zero;
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                for (int j = i + 1; j < eyes.Length; j++)
                {
                    if (eyes[j] == null) continue;
                    float d = Vector3.Distance(eyes[i].bounds.center, eyes[j].bounds.center);
                    if (d <= span) continue;
                    span = d;
                    a = eyes[i].bounds.center;
                    b = eyes[j].bounds.center;
                }
            }
            if (span < 1e-4f) return false;

            center = (a + b) * 0.5f;

            Vector3 right = (b - a) / span;
            Vector3 up = Vector3.ProjectOnPlane(center - mouth.bounds.center, right);
            if (up.sqrMagnitude < 1e-8f) return false;
            up.Normalize();

            rotation = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            return true;
        }

        private static int GhostLayer(PlayerGhost ghost)
        {
            Renderer[] rends = ghost.PlayerRenderers;
            if (rends != null)
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    if (rends[i] != null) return rends[i].gameObject.layer;
                }
            }
            return ghost.gameObject.layer;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        private static bool HasVisibleMesh(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) return true;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null && skinned.sharedMesh != null) return true;
            // Something else entirely (particles, a hat made of children). Let it through.
            return filter == null && skinned == null;
        }

        private static float NonZero(float v)
        {
            return Mathf.Abs(v) < 1e-5f ? 1f : v;
        }

        /// <summary>
        /// Diagnostics. LogDebug rather than a verbose-logging setting: it stays out of the way
        /// by default and anyone chasing a problem can turn Debug on in BepInEx.cfg.
        /// </summary>
        private static void Log(string message)
        {
            Plugin.Log.LogDebug(message);
        }
    }
}
