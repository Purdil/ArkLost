using UnityEngine.UIElements;

namespace Test
{
    [UxmlElement]
    public partial class DataButton : Button
    {
        [UxmlAttribute]
        public int ButtonIndex { get; set; }
    }
}