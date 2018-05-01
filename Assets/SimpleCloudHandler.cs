using Vuforia;
using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using UnityEngine.UI;
using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using System.Collections.Generic;
using SimpleJSON;

class RowEntry{
	public string tag;
	public string content;

	public RowEntry(string tag, string content){
		this.tag = tag;
		this.content = content;
	}
	public Dictionary<string, System.Object> ToDictionary() {
		Dictionary<string, System.Object> result = new Dictionary<string, System.Object>();
		result["tag"] = tag;
		result["content"] = content;
		return result;
	}
}

public class SimpleCloudHandler : MonoBehaviour, ICloudRecoEventHandler {
	private CloudRecoBehaviour mCloudRecoBehaviour;
	private bool mIsScanning = false;
	private string mTargetMetadata = "";
	private string status = "" ;
	public ImageTargetBehaviour ImageTargetTemplate;
	public VideoPlayer videoPlayer;
	public AudioSource audioSource;
	public InputField i;
	public GameObject reviewText;
	public GameObject review;
	public GameObject showReviews;
	public Text reviews;
	DependencyStatus dependencyStatus = DependencyStatus.UnavailableOther;

	// Use this for initialization
	void Start () {

		//Firebase code
		FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
			dependencyStatus = task.Result;
			if (dependencyStatus == DependencyStatus.Available) {
				InitializeFirebase();
			} else {
				Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
			}
		});
		//Firebase code ends

		// register this event handler at the cloud reco behaviour
		mCloudRecoBehaviour = GetComponent<CloudRecoBehaviour>();

		if (mCloudRecoBehaviour)
		{
			mCloudRecoBehaviour.RegisterEventHandler(this);
		}
	}

	//Firebase code
	protected virtual void InitializeFirebase() {
		FirebaseApp app = FirebaseApp.DefaultInstance;
		app.SetEditorDatabaseUrl("https://vadd-ef60b.firebaseio.com/");
		if (app.Options.DatabaseUrl != null) app.SetEditorDatabaseUrl(app.Options.DatabaseUrl);
	}

	//To save the review to cloud database
	public void save() 
	{	
		Debug.Log ("Saving to database");
		string content= i.text;
		i.text = "";
		char[] splitChar = { '\n' }; 
		string[] list = mTargetMetadata.Split ( splitChar );
		string tag = list [0];
		string key = FirebaseDatabase.DefaultInstance.GetReference("Rows").Push().Key;
		RowEntry e = new RowEntry(tag,content);
		Dictionary<string, System.Object> eVal = e.ToDictionary();
		Dictionary<string, System.Object> childUpdates = new Dictionary<string, System.Object>();
		childUpdates["/Rows/" + key] = eVal ;
		FirebaseDatabase.DefaultInstance.GetReference("Rows").UpdateChildrenAsync(childUpdates);

	}

	//To show all the reviews related to the particular product
	public void show()
	{
		char[] splitChar = { '\n' }; 
		string[] list = mTargetMetadata.Split ( splitChar );
		string tag = list [0];
		FirebaseDatabase.DefaultInstance
			.GetReference("Rows")
			.GetValueAsync().ContinueWith(task => {
				if (task.IsFaulted) {
					Debug.Log("Error in fetching database");
				}
				else if (task.IsCompleted) {
					DataSnapshot snapshot = task.Result;
					//Debug.Log( snapshot.GetRawJsonValue().ToString() ) ; 
					string res = snapshot.GetRawJsonValue().ToString() ;
					var N = JSON.Parse (res);
					var rows = N ["Rows"].Children ;
					int j = 1 ;
					string ans = "Some reviews.\n" ;
					foreach( var row in rows )
					{
						string tag1 = row["tag"] , content = row["content"] ;
						if( tag.Equals( tag1 ) )  
						{
							ans = ans + j.ToString() + ". " + content + "\n";
						}
						j++;
					}
					reviews.text = ans;
				}
			});
	}
	//Firebase code ends

	public void OnInitialized() {
		showReviews.SetActive( false ) ;
		review.SetActive( false ) ;
		reviewText.SetActive (false);
		Debug.Log ("Cloud Reco initialized");
	}
	public void OnInitError(TargetFinder.InitState initError) {
		Debug.Log ("Cloud Reco init error " + initError.ToString());
	}
	public void OnUpdateError(TargetFinder.UpdateState updateError) {
		Debug.Log ("Cloud Reco update error " + updateError.ToString());
	}

	public void OnStateChanged(bool scanning) {
		mIsScanning = scanning;
		if (scanning)
		{
			// clear all known trackables
			var tracker = TrackerManager.Instance.GetTracker<ObjectTracker>();
			tracker.TargetFinder.ClearTrackables(false);
		}
	}

	// Here we handle a cloud target recognition event
	public void OnNewSearchResult(TargetFinder.TargetSearchResult targetSearchResult) {
		// do something with the target metadata
		showReviews.SetActive( true ) ;
		review.SetActive( true ) ;
		reviewText.SetActive (true);
		mTargetMetadata = targetSearchResult.MetaData;
		// stop the target finder (i.e. stop scanning the cloud)
		mCloudRecoBehaviour.CloudRecoEnabled = false;

		// Build augmentation based on target
		if (ImageTargetTemplate) {
			// enable the new result with the same ImageTargetBehaviour:
			ObjectTracker tracker = TrackerManager.Instance.GetTracker<ObjectTracker>();
			ImageTargetBehaviour imageTargetBehaviour =(ImageTargetBehaviour)tracker.TargetFinder.EnableTracking(targetSearchResult, ImageTargetTemplate.gameObject);
			StartCoroutine (playVideo ());
		}
	}

	IEnumerator playVideo()
	{
		audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.Pause();
		videoPlayer.source = VideoSource.Url;
		string temp = mTargetMetadata;
		temp = temp.Remove (temp.Length - 1);

		char[] splitChar = { '\n' }; 
		string[] list = mTargetMetadata.Split ( splitChar );
		videoPlayer.url = list[1] ;
		//videoPlayer.url = "http://www.quirksmode.org/html5/videos/big_buck_bunny.mp4";
		videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
		videoPlayer.EnableAudioTrack(0, true);
		videoPlayer.SetTargetAudioSource(0, audioSource);
		videoPlayer.Prepare();
		WaitForSeconds waitTime = new WaitForSeconds(1);
		while (!videoPlayer.isPrepared)
		{
			status = "Preparing Video" ;
			yield return waitTime;
			break;
		}
		status = "Done Preparing Video" ;
		videoPlayer.Play();
		audioSource.Play();
		status = "Playing Video" ;
		while (videoPlayer.isPlaying)
		{
			yield return null;
		}
		status = "Done Playing Video" ;
	}

	void OnGUI() {
		// Display current 'scanning' status
		GUI.Box (new Rect(100,100,200,50), mIsScanning ? "Scanning" : "Not scanning");
		// Display metadata of latest detected cloud-target
		GUI.Box (new Rect(100,200,200,50), "Metadata: " + mTargetMetadata);
		GUI.Box (new Rect(100,400,200,50), "Video Staus : " + status);

		// If not scanning, show button
		// so that user can restart cloud scanning
		if (!mIsScanning) {
			if (GUI.Button(new Rect(100,300,200,50), "Restart Scanning")) {
				// Restart TargetFinder
				showReviews.SetActive( false ) ;
				review.SetActive( false ) ;
				reviewText.SetActive (false);
				reviews.text = "";
				mCloudRecoBehaviour.CloudRecoEnabled = true;
			}
		}
	}
}