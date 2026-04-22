using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PrivacyInfomation : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private HyperlinkText descriptionText;
    [SerializeField] private Button closeButton;

    private string _privacyPolicy;
    private string _agreement;
    [SerializeField]private string _resource;
    // Start is called before the first frame update
    void Awake()
    {
        _privacyPolicy = MultilingualManager.Instance.GetString("UserPrivacyPolicy");
        _agreement = MultilingualManager.Instance.GetString("UserAgreement");
        
        closeButton.AddClickAction(OnCloseClicked);
        closeButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("PrivacyAgreement03");
    }

    public void SetOpenData(string resource, string url)
    {
        _resource = resource;
        if (url.Contains("yhxy") || url.Contains("yhxyb"))
        {
            titleText.text = MultilingualManager.Instance.GetString("TermsAndService");
            descriptionText.text = _agreement;
        }
        else
        {
            titleText.text = MultilingualManager.Instance.GetString("PrivacyPolicy");
            descriptionText.text = _privacyPolicy;
        }
    }

    private void OnCloseClicked()
    {
        GameObject pg;
        if (_resource.Contains("PrivacyReject"))
        {
            pg = Resources.Load<GameObject>("Privacy/PrivacyReject");
            GameObject ps = Instantiate(pg, transform.parent);
            ps.SetActive(true);
        }
           
        else if (_resource.Contains("OptionsView"))
        {
            Debug.Log("什么也不做");
        }
        else
        {
            pg = Resources.Load<GameObject>("Privacy/PrivacyGuidance");
            GameObject ps = Instantiate(pg, transform.parent);
            ps.SetActive(true);
        }
        Destroy(gameObject);
    }
}
