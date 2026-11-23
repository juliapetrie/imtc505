using System;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class LivingRoomCouch
{
    private readonly GameObject group;
    private readonly Transform guestGroup;
    private readonly bool ownsGuests;
    private readonly Material bowlMaterial;
    private readonly Material popcornMaterial;
    private readonly Transform[] heads = new Transform[2];
    private readonly Transform[] arms = new Transform[2];
    private readonly Quaternion[] headRest = new Quaternion[2];
    private readonly Quaternion[] armRest = new Quaternion[2];
    private readonly Transform[] seats = new Transform[2];
    private readonly PartyMotion[] motions = new PartyMotion[2];
    private readonly Transform[] pelvis = new Transform[2];
    private readonly Vector3[] seatPoints = new Vector3[2];
    private float time;

    public LivingRoomCouch(Transform room, GameObject[] models, AnimationClip sitting, LivingRoomMixamo cast, bool authoring = false)
    {
        guestGroup = room.Find("Couch Guests");
        ownsGuests = guestGroup == null;
        if (ownsGuests)
        {
            guestGroup = new GameObject("Couch Guests").transform;
            guestGroup.SetParent(room, false);
        }
        if (!authoring)
        {
            group = new GameObject("Couch Popcorn");
            group.transform.SetParent(room, false);
        }
        if (sitting == null || models == null || models.Length < 2)
        {
            Debug.LogWarning("Couch guests need two character models and a sitting clip.", group);
            return;
        }

        bool useMixamo = cast.Models != null && cast.Sit != null;
        CreateGuest(useMixamo ? cast.SeatedModel ?? cast.Models[0] : models[0],
            useMixamo ? cast.Sit : sitting, 0, 0.29f, useMixamo, authoring);
        CreateGuest(useMixamo ? cast.Models[Mathf.Min(1, cast.Models.Length - 1)] : models[1],
            useMixamo ? cast.Sit : sitting, 1, 0.91f, useMixamo, authoring);
        if (authoring)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bowlMaterial = new Material(shader) { color = new Color(0.64f, 0.13f, 0.26f) };
        popcornMaterial = new Material(shader) { color = new Color(1f, 0.88f, 0.59f) };
        Vector3 bowl = new Vector3(-0.975f, -0.158f, 0.60f);
        AddProp("Popcorn Bowl", bowl, new Vector3(0.19f, 0.087f, 0.19f), bowlMaterial);

        var random = new System.Random(35);
        for (int i = 0; i < 19; i++)
        {
            float angle = i * 2.399963f;
            float radius = 0.076f * Mathf.Sqrt(i / 19f);
            float height = 0.038f + (float)random.NextDouble() * 0.024f - radius * 0.20f;
            Vector3 point = bowl + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            AddProp("Popcorn", point, new Vector3(0.036f, 0.031f, 0.036f), popcornMaterial);
        }
    }

    private void CreateGuest(GameObject source, AnimationClip sitting, int index, float z, bool useMixamo, bool authoring)
    {
        if (source == null)
            return;

        string label = "Seated Guest " + (index + 1);
        Transform seat = ownsGuests ? new GameObject(label).transform : guestGroup.Find(label);
        if (seat == null || (!ownsGuests && seat.childCount == 0))
            return;

        GameObject figure;
        if (ownsGuests)
        {
            seat.SetParent(guestGroup, false);
            seat.localPosition = new Vector3(-1.035f, -0.105f, z);
            seat.localRotation = Quaternion.Euler(0f, index == 0 ? 74f : 106f, 0f);
            figure = LivingRoomGuests.InstantiateGuestModel(source, seat, authoring);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.identity;
            LivingRoomGuests.FitModel(figure.transform, 1.30f);
        }
        else
        {
            figure = seat.GetChild(0).gameObject;
        }

        seats[index] = seat;
        Vector3 hipPosition = seat.localPosition;
        seatPoints[index] = hipPosition;
        if (authoring)
        {
            sitting.SampleAnimation(figure, sitting.length * (index == 0 ? 0.13f : 0.57f));
            var previewAnimator = figure.GetComponentInChildren<Animator>();
            if (previewAnimator != null && previewAnimator.isHuman)
                seat.localPosition += hipPosition
                    - guestGroup.InverseTransformPoint(previewAnimator.GetBoneTransform(HumanBodyBones.Hips).position);
            return;
        }

        Animator animator = figure.GetComponentInChildren<Animator>();
        if (!ownsGuests && animator != null && animator.isHuman)
        {
            Transform authoredHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (authoredHips != null)
                seatPoints[index] = guestGroup.InverseTransformPoint(authoredHips.position);
        }

        if (useMixamo && animator != null)
        {
            var motion = new PartyMotion(animator);
            motions[index] = motion;
            motion.Set(sitting, index == 0 ? 0.13f : 0.57f);
            motion.Update(0f);
            pelvis[index] = animator.GetBoneTransform(HumanBodyBones.Hips);
            heads[index] = animator.GetBoneTransform(HumanBodyBones.Head);
            return;
        }

        if (animator != null)
            animator.enabled = false;
        sitting.SampleAnimation(figure, sitting.length * (index == 0 ? 0.38f : 0.59f));

        Transform hips = null;
        foreach (Transform bone in figure.GetComponentsInChildren<Transform>())
        {
            if (bone.name == "Hips")
                hips = bone;
            else if (bone.name == "Head")
                heads[index] = bone;
            else if (bone.name == (index == 0 ? "UpperArm.L" : "UpperArm.R"))
                arms[index] = bone;
        }
        if (hips != null)
            seat.localPosition += hipPosition - guestGroup.InverseTransformPoint(hips.position);
        if (heads[index] != null)
            headRest[index] = heads[index].localRotation;
        if (arms[index] != null)
            armRest[index] = arms[index].localRotation;
    }

    private void AddProp(string name, Vector3 center, Vector3 size, Material material)
    {
        GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prop.name = name;
        prop.transform.SetParent(group.transform, false);
        prop.transform.localPosition = center;
        prop.transform.localScale = size;
        Collider collider = prop.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        prop.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public void Update(float elapsed)
    {
        time += elapsed;
        for (int i = 0; i < motions.Length; i++)
        {
            if (motions[i] == null)
                continue;
            motions[i].Update(elapsed);
            if (pelvis[i] != null)
                seats[i].localPosition += seatPoints[i] - guestGroup.InverseTransformPoint(pelvis[i].position);
            if (heads[i] != null)
                headRest[i] = heads[i].localRotation;
        }
        for (int i = 0; i < heads.Length; i++)
        {
            if (heads[i] == null || heads[1 - i] == null)
                continue;

            Transform head = heads[i];
            head.localRotation = headRest[i];
            Vector3 direction = heads[1 - i].position - head.position;
            Quaternion aim = Quaternion.FromToRotation(seats[i].forward, direction);
            float glance = 0.52f + 0.27f * Mathf.Sin(time * 0.83f + i * 1.7f);
            head.rotation = Quaternion.Slerp(Quaternion.identity, aim, glance) * head.rotation
                * Quaternion.Euler(Mathf.Sin(time * 1.8f + i) * 3f, 0f, 0f);

            Transform arm = arms[i];
            if (arm != null)
            {
                arm.localRotation = armRest[i];
                float gesture = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.15f + i * Mathf.PI)), 4f);
                arm.rotation = Quaternion.AngleAxis(-9f * gesture, seats[i].right) * arm.rotation;
            }
        }
    }

    public void Dispose()
    {
        foreach (PartyMotion motion in motions) motion?.Dispose();
        Object.Destroy(group);
        if (ownsGuests) Object.Destroy(guestGroup.gameObject);
        Object.Destroy(bowlMaterial);
        Object.Destroy(popcornMaterial);
    }
}
