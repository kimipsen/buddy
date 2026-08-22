<#-- Branded wrapper for all HTML emails. Keycloak's base theme keeps this macro -->
<#-- (emailLayout) minimal ("<html><body><#nested></body></html>"); every content -->
<#-- template (email-verification.ftl, password-reset.ftl, ...) is inherited -->
<#-- unchanged from base and just imports this file, so restyling only this macro -->
<#-- re-skins every email without duplicating their message logic. -->
<#macro emailLayout>
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title></title>
<!--[if mso]>
<style type="text/css">
table { border-collapse: collapse; }
</style>
<![endif]-->
<style>
  body, table, td { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
  p { margin: 0 0 16px 0; }
  p:last-child { margin-bottom: 0; }
  a { color: #047857; }
</style>
</head>
<body style="margin:0; padding:0; background-color:#f1f5f9;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f1f5f9;">
<tr>
<td align="center" style="padding:40px 16px;">

<table role="presentation" width="480" cellpadding="0" cellspacing="0" style="width:100%; max-width:480px; background-color:#ffffff; border:1px solid #e2e8f0; border-radius:8px;">
<tr>
<td style="padding:32px 32px 24px 32px; text-align:center;">
<table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 auto;">
<tr>
<td style="vertical-align:middle; padding-right:10px;">
<table role="presentation" width="40" height="40" cellpadding="0" cellspacing="0" style="width:40px; height:40px; background-color:#34d399; border-radius:8px;">
<tr><td align="center" valign="middle" style="color:#020617; font-weight:600; font-size:16px;">B</td></tr>
</table>
</td>
<td style="vertical-align:middle;">
<span style="font-size:18px; font-weight:600; color:#020617;">Buddy</span>
</td>
</tr>
</table>
</td>
</tr>
<tr>
<td style="padding:0 32px 32px 32px; color:#0f172a; font-size:14px; line-height:22px; text-align:left;">
<#nested>
</td>
</tr>
</table>

<table role="presentation" width="480" cellpadding="0" cellspacing="0" style="width:100%; max-width:480px;">
<tr>
<td style="padding:20px 32px; text-align:center; color:#94a3b8; font-size:12px; line-height:18px;">
This is an automated message from Buddy &mdash; please don't reply to this email.
</td>
</tr>
</table>

</td>
</tr>
</table>
</body>
</html>
</#macro>
