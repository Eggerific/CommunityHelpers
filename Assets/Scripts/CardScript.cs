using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;


public class CardScript : MonoBehaviour
{
    public CardPlaceSlot slotPull = null;
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

        //Debug.Log("ok");
        if (slotPull && ok)
        {
            Debug.Log("HEY");
        }
    }
}
