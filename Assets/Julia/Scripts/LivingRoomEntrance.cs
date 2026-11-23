using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

internal sealed class LivingRoomEntrance
{
    private const float DoorCenter = -1.4255f;
    private const float DoorLeft = DoorCenter - 0.36f;
    private const float Floor = -0.475f;
    private const float WallZ = -1.375f;
    private readonly Transform room;
    private readonly GameObject rig;
    private readonly Transform door;
    private readonly Transform hinge;
    private readonly Transform originalDoorParent;
    private readonly Vector3 originalDoorPosition;
    private readonly Quaternion originalDoorRotation;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private readonly MeshRenderer[] windowPanes;
    private readonly MaterialPropertyBlock windowProperties = new();
    private readonly Collider knockTarget;
    private readonly TextMesh prompt;
    private readonly Transform host;
    private readonly Animator animator;
    private readonly PartyMotion hostMotion;
    private readonly LivingRoomMixamo cast;
    private readonly Vector3[] route;
    private int waypoint;
    private float opening;
    private float elapsedSinceKnock;
    private bool knocked;

    public LivingRoomEntrance(Transform room, Transform booth, GameObject model,
        RuntimeAnimatorController controller, LivingRoomMixamo cast, MeshRenderer[] windowPanes)
    {
        this.room = room;
        this.cast = cast;
        this.windowPanes = windowPanes;
        rig = new GameObject("Living Room Entrance");
        rig.transform.SetParent(room, false);
        foreach (MeshFilter part in room.GetComponentsInChildren<MeshFilter>())
        {
            if (part.name != "mesh1702814703")
                continue;
            door = part.transform;
            break;
        }

        if (door != null)
        {
            originalDoorParent = door.parent;
            originalDoorPosition = door.localPosition;
            originalDoorRotation = door.localRotation;
            hinge = Child("Door Hinge", new Vector3(DoorLeft, Floor, WallZ));
            door.SetParent(hinge, true);
            var leaf = door.gameObject.AddComponent<BoxCollider>();
            leaf.center = door.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            leaf.size = door.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            knockTarget = leaf;
            var knock = door.gameObject.AddComponent<XRSimpleInteractable>();
            knock.selectEntered.AddListener(_ => Knock());
        }

        Transform instructions = Child("Knock Prompt", new Vector3(DoorCenter, 0.73f, -1.60f));
        prompt = instructions.gameObject.AddComponent<TextMesh>();
        prompt.anchor = TextAnchor.MiddleCenter;
        prompt.alignment = TextAlignment.Center;
        prompt.fontSize = 64;
        prompt.characterSize = 0.005f;
        prompt.color = new Color(0.92f, 0.88f, 1f);
        prompt.gameObject.SetActive(false);

        host = Child("DJ Host", new Vector3(-0.62f, Floor, -0.86f));
        if (model != null)
        {
            GameObject figure = Object.Instantiate(model, host, false);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.identity;
            animator = figure.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            LivingRoomGuests.FitModel(figure.transform, 1.34f);
            if (cast.Models != null && animator != null && animator.isHuman)
                hostMotion = new PartyMotion(animator);
        }
        Vector3 deck = room.InverseTransformPoint(booth.position);
        route = new[]
        {
            new Vector3(-1.03f, Floor, -0.72f),
            new Vector3(-1.83f, Floor, -0.74f),
            new Vector3(deck.x - 0.58f, Floor, -0.72f),
            new Vector3(deck.x - 0.58f, Floor, deck.z - 0.28f)
        };
    }

    private Transform Child(string name, Vector3 position)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(rig.transform, false);
        child.localPosition = position;
        return child;
    }

    private void Knock()
    {
        Camera player = Camera.main;
        if (!knocked && player != null && NearDoor(room.InverseTransformPoint(player.transform.position)))
            knocked = true;
    }

    private static bool NearDoor(Vector3 player)
    {
        return player.z < -1.30f && player.z > -3.10f && Mathf.Abs(player.x - DoorCenter) < 0.95f;
    }

    public void Update(float dt, float partyPhase)
    {
        if (hinge == null)
            return;

        Color glow = Color.Lerp(new Color(0.34f, 0.24f, 0.91f, 0.09f),
            new Color(0.67f, 0.28f, 0.92f, 0.09f), 0.5f + 0.5f * Mathf.Sin(partyPhase * 2f * Mathf.PI));
        if (windowPanes != null)
        {
            foreach (MeshRenderer pane in windowPanes)
            {
                if (pane == null)
                    continue;
                pane.GetPropertyBlock(windowProperties);
                windowProperties.SetColor(BaseColor, glow);
                pane.SetPropertyBlock(windowProperties);
            }
        }
        Camera player = Camera.main;
        if (player == null)
            return;

        Vector3 local = room.InverseTransformPoint(player.transform.position);
        bool nearby = NearDoor(local);
        if (!knocked && nearby && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = player.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (knockTarget.Raycast(ray, out _, 2.2f))
                Knock();
        }

        if (knocked)
        {
            elapsedSinceKnock += dt;
            if (elapsedSinceKnock > 0.6f)
            {
                opening = Mathf.MoveTowards(opening, 1f, dt * 1.45f);
                hinge.localRotation = Quaternion.Euler(0f, -96f * Mathf.SmoothStep(0f, 1f, opening), 0f);
            }
        }

        bool entered = local.z > -1.29f;
        string message = !knocked ? "Knock on the door\nSelect the door"
            : opening < 0.85f ? "Coming!"
            : !entered && waypoint == 0 ? "You're our DJ tonight.\nCome on in!"
            : waypoint < route.Length ? "Follow me to the DJ booth"
            : "You're up!\nSelect the lit pad to start";
        bool show = nearby || (knocked && entered && local.x > -3.4f && local.z < 2.5f
            && (waypoint < route.Length || Mathf.Abs(local.x - host.localPosition.x) < 1.15f && Mathf.Abs(local.z - host.localPosition.z) < 1.15f));
        prompt.gameObject.SetActive(show);
        if (show)
        {
            prompt.text = message;
            prompt.transform.localPosition = !entered
                ? new Vector3(DoorCenter, 0.73f, -1.60f)
                : host.localPosition + Vector3.up * 1.42f;
            Vector3 view = prompt.transform.position - player.transform.position;
            if (view.sqrMagnitude > 0.01f)
                prompt.transform.rotation = Quaternion.LookRotation(view);
        }

        bool walking = false;
        Vector3 facing = local - host.localPosition;
        if (knocked && !entered && waypoint == 0)
        {
            Vector3 doorSide = new Vector3(-0.79f, Floor, -1.03f);
            walking = MoveHost(doorSide, local, dt * 0.50f);
        }
        if (knocked && entered && waypoint < route.Length
            && Vector2.Distance(new Vector2(local.x, local.z), new Vector2(host.localPosition.x, host.localPosition.z)) < 1.45f)
        {
            Vector3 next = route[waypoint];
            facing = next - host.localPosition;
            walking = MoveHost(next, local, dt * 0.62f);
            if ((next - host.localPosition).sqrMagnitude < 0.0025f)
                waypoint++;
        }
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0025f)
            host.localRotation = Quaternion.RotateTowards(host.localRotation, Quaternion.LookRotation(facing), dt * 240f);
        if (hostMotion != null)
        {
            hostMotion.Set(walking ? cast.Walk : cast.Talk(0));
            hostMotion.Update(dt);
        }
        else if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetFloat("Speed", walking ? 1f : 0f);
    }

    private bool MoveHost(Vector3 destination, Vector3 player, float distance)
    {
        Vector3 current = host.localPosition;
        Vector3 next = Vector3.MoveTowards(current, destination, distance);
        if (LivingRoomGuests.EntersPlayerSpace(current, next, player))
            return false;

        host.localPosition = next;
        return (next - current).sqrMagnitude > 0.000001f;
    }

    public void Dispose()
    {
        hostMotion?.Dispose();
        if (door != null)
        {
            door.SetParent(originalDoorParent, false);
            door.localPosition = originalDoorPosition;
            door.localRotation = originalDoorRotation;
            Object.Destroy(door.GetComponent<XRSimpleInteractable>());
            Object.Destroy(knockTarget);
        }
        Object.Destroy(rig);
    }
}
