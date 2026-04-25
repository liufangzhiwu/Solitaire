using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class PrivacyReject : MonoBehaviour
{
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button _callbackButton;
    [SerializeField] private Button _rejectButton;
    // Start is called before the first frame update
    void Start()
    {
        _descriptionText.text = MultilingualManager.Instance.GetString("PrivacyAgreement05");
        _descriptionText.onHyperlinkClick = OnClickText;
        _callbackButton.AddClickAction(OnCallbackClick);
        _rejectButton.AddClickAction(OnRejectClick);
        _callbackButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("PrivacyAgreement06");
        _rejectButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("PrivacyAgreement07");
    }

    private void OnRejectClick()
    {
        Application.Quit();
    }

    private void OnCallbackClick()
    {
        gameObject.SetActive(false);
        transform.parent.GetComponentInChildren<PrivacyGuidance>(true).gameObject.SetActive(true);
    }
    
    void OnClickText(string url)
    {
        // if (!Game.IsNetworkActive)
        // {
        //     GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyInfomation");
        //     GameObject pi = Instantiate(pg, transform.parent);
        //     pi.GetComponent<PrivacyInfomation>().SetOpenData(this.name, url);
        //     pi.SetActive(true);
        //     Destroy(gameObject);
        // }
        // else
            Application.OpenURL(url);
    }
}
