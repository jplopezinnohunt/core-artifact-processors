namespace VendorMdm.Shared.Services.EmailTemplates
{
    public interface IEmailTemplate<T> where T : class
    {
        string GetSubject(T data);
        string GetHtmlBody(T data);
    }
}
