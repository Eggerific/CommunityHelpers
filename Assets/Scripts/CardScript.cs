using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;


public class CardScript : MonoBehaviour
{
    public CardPlaceSlot slotPull = null;

    // Which slot this card belongs in, e.g. "Doctor/Tool". Matches CardPlaceSlot.cardCategory.
    public string cardCategory;

    private bool cardLock = false;

    // Found anywhere under this card (e.g. the HandGrab building block child).
    // Cards without one (such as a card pre-placed in a slot) are treated as never held.
    private GrabInteractable grabInteractable;         // controller grabs
    private HandGrabInteractable handGrabInteractable; // hand-tracking grabs

    [Tooltip("If the card ends up this far (m) below its starting spot, e.g. dropped off the table, it pops back there.")]
    public float returnHomeDrop = 1f;

    // Starting spot, relative to the parent (the table), so it follows the table when SeatAnchor moves the room.
    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Rigidbody body;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        grabInteractable = GetComponentInChildren<GrabInteractable>();
        handGrabInteractable = GetComponentInChildren<HandGrabInteractable>();
        body = GetComponent<Rigidbody>();
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
    }

    private void ReturnHome()
    {
        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // ok == true means no hand/controller is currently grabbing the card
        bool ok = true;
        if (grabInteractable != null)
        {
            foreach (GrabInteractor interactor in grabInteractable.Interactors) { ok = false; break; }
        }
        if (ok && handGrabInteractable != null)
        {
            foreach (HandGrabInteractor interactor in handGrabInteractable.Interactors) { ok = false; break; }
        }

        // Safety net: the room has no floor collider, so a dropped card would fall forever.
        if (ok && !cardLock && homeParent != null &&
            transform.position.y < homeParent.TransformPoint(homeLocalPosition).y - returnHomeDrop)
        {
            ReturnHome();
        }

        if (cardLock)
        {
            if (ok)
            {
                gameObject.transform.position = slotPull.gameObject.transform.position;
                gameObject.transform.rotation = slotPull.gameObject.transform.rotation;
            }
        } else
        {
            //Debug.Log("ok");
            if (slotPull && ok)
            {
                gameObject.transform.parent = slotPull.gameObject.transform;
                LockInPlace();
            }
        }

    }

    /// <summary>Puts the card straight into a slot and locks it there (used when the game lays out the helpers).</summary>
    public void PlaceInSlot(CardPlaceSlot sp)
    {
        slotPull = sp;
        transform.SetParent(sp.transform, false);
        LockInPlace();
    }

    // Snap onto the slot and stop physics on the card. Stacked cards overlap a little, so a card left
    // under gravity would jitter and shove its neighbors. Grabbing still works: the Grabbable puts the
    // card back to kinematic when it's let go, and Update snaps it back onto the slot.
    private void LockInPlace()
    {
        cardLock = true;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        var rb = body != null ? body : GetComponent<Rigidbody>();
        if (rb == null) return;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
    }

    public void SetPull(CardPlaceSlot sp)
    {
        if (!cardLock) slotPull = sp;
    }
    public void ResetPull()
    {
        if (!cardLock) slotPull = null;
    }
}
