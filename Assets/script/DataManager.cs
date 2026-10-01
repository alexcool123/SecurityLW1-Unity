using UnityEngine;
using System.IO;
using TMPro;
using System.Collections.Generic;
public class DataManager : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField NameField;
    public TMP_InputField SurnameField;
    public TMP_InputField YearField;
    public TMP_InputField PhoneField;
    public TMP_InputField StudentIDField;

    public GameObject TextObject;


    public void ShowText() 
    { 
       if (TextObject != null)
       {
            TextObject.SetActive(true);
       }

    }



    public void SaveData()
    {
        string dbPath = Path.Combine(Application.persistentDataPath, "my_database.json");

        PersonalData db = new PersonalData ();

        if(File.Exists(dbPath))
        {
            string json = File.ReadAllText(dbPath);
            db = JsonUtility.FromJson<PersonalData>(json);
        }

        db.firstName = NameField.text;
        db.lastName = SurnameField.text;
        db.birthYear = int.Parse(YearField.text);
        db.phone = PhoneField.text;
        db.studentId = StudentIDField.text; 
        


        string jsonData = JsonUtility.ToJson(db);
        File.WriteAllText(dbPath, jsonData);
        Debug.Log("Данные сохранены в JSON по пути: " + dbPath);
    }

    

}



