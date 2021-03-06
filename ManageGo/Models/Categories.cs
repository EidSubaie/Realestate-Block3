using System;
using Newtonsoft.Json;
using PropertyChanged;

namespace ManageGo
{
    [AddINotifyPropertyChangedInterface, Serializable]
    public class Categories
    {
        public string CategoryName { get; set; }
        public int CategoryID { get; set; }
        public string Color { get; set; }

        [JsonIgnore, AlsoNotifyFor("CheckBoxImage")]
        public bool IsSelected { get; set; }
        //for filtering multiple can be selected

        [JsonIgnore]
        public string CheckBoxImage
        {
            get
            {
                return IsSelected ? "checked.png" : "unchecked.png";
            }
        }


        [JsonIgnore]
        public bool IsEnabled { get; set; } = true;
    }
}
