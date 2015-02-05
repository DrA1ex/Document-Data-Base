using System.ComponentModel;
using DataLayer.Attributes;

namespace DataLayer.Model
{
    public enum DocumentType
    {
        [Description("Текстовый документ")]
        [Extension("txt", "cpp", "h", "cs")]
        Text,

        [Description("Документ MS Word 97-2003")]
        [Extension("doc")]
        Doc,

        [Description("Документ MS Word 2007")]
        [Extension("docx")]
        DocX,

        [Description("Документ RTF")]
        [Extension("rtf")]
        Rtf,

        [Description("Документ PDF")]
        [Extension("pdf")]
        Pdf,

        [Description("Аудиотрек")]
        [Extension("mp3", "ogg", "flac", "wav")]
        Audio,

        [Description("Видеоклип")]
        [Extension("avi", "mkv", "mp4", "flv", "mov")]
        Video,

        [Description("Неподдерживаемый тип")]
        Undefined = -1
    }
}
