using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;


public class CardScript : MonoBehaviour
{
    public CardPlaceSlot slotPull = null;

    private bool cardLock = false;
    //private IEnumerable<GrabInteractor> setInteractors = [];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        IEnumerable<GrabInteractor> setInteractors = transform.GetChild(0).GetComponent<GrabInteractable>().Interactors;

        bool ok = true;
        foreach (GrabInteractor interactor in setInteractors) { ok = false; break; }

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
                cardLock = true;
            }
        }

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
