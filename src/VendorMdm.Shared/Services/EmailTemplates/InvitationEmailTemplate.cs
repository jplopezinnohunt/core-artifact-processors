using VendorMdm.Shared.Models.Email;

namespace VendorMdm.Shared.Services.EmailTemplates
{
    public class InvitationEmailTemplate : IEmailTemplate<InvitationEmailData>
    {
        public string GetSubject(InvitationEmailData data)
        {
            return $"Invitation to {data.CompanyName} Vendor Portal";
        }

        public string GetHtmlBody(InvitationEmailData data)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078d4; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #0078d4; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{data.CompanyName}</h1>
            <p>Vendor Portal Invitation</p>
        </div>
        <div class='content'>
            <p>Hello {data.VendorName},</p>
            <p>You have been invited by {data.InvitedByName} to register as a vendor in our portal.</p>
            <p>Click the button below to complete your registration:</p>
            <p style='text-align: center;'>
                <a href='{data.InvitationLink}' class='button'>Accept Invitation</a>
            </p>
            <p><strong>Important:</strong> This invitation link will expire on {data.ExpiresAt}.</p>
            <p>If you have any questions, please contact your invitation sender.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
