USE [master];
GO

IF DB_ID(N'HelpDesk') IS NULL
    EXEC(N'CREATE DATABASE [HelpDesk];');
GO

USE [HelpDesk];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Policies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Policies (
        Citation nvarchar(40) NOT NULL CONSTRAINT PK_Policies PRIMARY KEY,
        Question nvarchar(240) NOT NULL CONSTRAINT UQ_Policies_Question UNIQUE,
        Answer nvarchar(1000) NOT NULL,
        Keywords nvarchar(400) NOT NULL,
        InsufficientInformation bit NOT NULL CONSTRAINT DF_Policies_InsufficientInformation DEFAULT (0)
    );
END;

INSERT INTO dbo.Policies (Citation, Question, Answer, Keywords, InsufficientInformation)
SELECT seed.Citation, seed.Question, seed.Answer, seed.Keywords, seed.InsufficientInformation
FROM (VALUES
    (N'IT Policy §4.2', N'Can I install Zoom on my work laptop?', N'The excerpt does not say whether Zoom is in the approved software catalog. If Zoom is listed, you may install it. If it is not listed, get IT approval first. This rule applies to any software installed on a work computer.', N'install installing software zoom catalog applications apps', 1),
    (N'IT Policy §4.3', N'How long does it take IT to approve a software request?', N'The software approval policy gives no fixed approval timeline. IT reviews requests for software outside the approved catalog. An initial help desk reply and a software approval decision are separate.', N'approve approval approvals turnaround timeline decision timeframe software', 1),
    (N'IT Policy §2.1', N'How long does it take help desk to reply to my request?', N'It typically takes 2 hours for IT to respond to service requests. This is the expected initial reply time. Resolution times vary with the issue.', N'reply replies respond response responds sla service turnaround', 0),
    (N'IT Policy §3.1', N'How do I reset a forgotten password?', N'Use the self-service password reset portal and complete identity verification. If you cannot verify your identity, contact the help desk. Never send your password in a ticket.', N'reset resetting forgotten forgot password passwords', 0),
    (N'IT Policy §3.2', N'How do I move MFA to a new phone?', N'If you still have your old phone, add the new device in Security Info before removing the old one. If you cannot sign in, contact IT for identity verification and MFA recovery.', N'mfa authenticator authentication factor phone mobile', 0),
    (N'IT Policy §5.1', N'What should I do if the VPN will not connect?', N'Check your internet connection, reopen the company VPN client, and try signing in again. If it still fails, include the VPN error message and timestamp in an IT ticket.', N'vpn reconnect tunnel tunnels', 0),
    (N'IT Policy §5.2', N'How do I get guest Wi-Fi for a visitor?', N'Connect visitors to the company guest network. A host can request a one-day access code from reception. Guest Wi-Fi has internet access only.', N'guest visitor visitors reception wifi wireless', 0),
    (N'IT Policy §5.3', N'How do I connect to employee Wi-Fi?', N'On a managed laptop, select the corporate wireless network and sign in with your company account. Contact IT if the network is absent from the managed profile.', N'employee corporate managed wifi wireless network', 0),
    (N'IT Policy §6.1', N'How do I report a phishing email?', N'Use the Report Phishing button in your mail client. If it is unavailable, forward the original message as an attachment to the security team. Avoid opening links or attachments.', N'phishing suspicious scam email message report', 0),
    (N'IT Policy §6.2', N'What should I do if my work laptop is lost or stolen?', N'Contact the IT security desk immediately and provide the last known location and time. IT can lock the device and revoke active sessions.', N'lost stolen theft missing laptop device', 0),
    (N'IT Policy §7.1', N'How do I add a shared office printer?', N'Open the managed printer portal and choose the printer by its office location and asset label. Contact IT with the label if it does not appear.', N'printer printers print printing toner', 0),
    (N'IT Policy §7.2', N'How do I request equipment for a new employee?', N'The hiring manager should submit an onboarding request at least five business days before the start date. Include the employee''s role, location, start date, and required equipment.', N'onboarding hire hiring starter equipment employee', 0),
    (N'IT Policy §8.1', N'How do I request access to a shared mailbox?', N'Open an access request with the mailbox address and the requested permissions. The mailbox owner must approve access before IT grants it.', N'shared mailbox mailboxes delegate delegation permissions', 0),
    (N'IT Policy §8.2', N'Can IT recover a deleted file?', N'Check the recycle bin or version history first. If the file was stored in a company-managed location, submit a restore request with its name, location, and approximate deletion date.', N'restore recover deleted deletion recycle file files backup', 0),
    (N'IT Policy §4.4', N'Can I postpone a required restart after an update?', N'You may postpone a standard update restart until the deadline shown in the update prompt. Save your work before that deadline. Urgent security updates can have a shorter deadline.', N'restart reboot update updates postpone patch patches', 0),
    (N'IT Policy §7.3', N'How do I request a replacement for broken hardware?', N'Create a hardware ticket with the device asset tag, symptoms, and your location. IT will diagnose the issue before issuing a repair or replacement.', N'broken hardware replacement replace repair damaged asset', 0),
    (N'IT Policy §6.3', N'Can I use a USB drive for company files?', N'Use company-issued encrypted USB drives for company files. A personal removable drive requires an approved exception from the security team.', N'usb removable thumb flash drive drives encrypted', 0),
    (N'IT Policy §5.4', N'How do I request remote desktop access?', N'Submit an access request with the target computer, business reason, and manager approval. Use the company remote access gateway after IT grants permission.', N'remote desktop rdp gateway', 0),
    (N'IT Policy §3.3', N'What do I do if my account is locked?', N'If your company account is locked after repeated sign-in attempts, wait 15 minutes before trying once more. Use the self-service unlock option or contact IT if you remain locked out.', N'lockout locked unlock account sign-in', 0),
    (N'IT Policy §2.2', N'How do I reopen a closed help desk ticket?', N'Reply to the ticket confirmation or reopen the ticket in the service portal within five business days. After that, open a new ticket and reference the old ticket number.', N'reopen closed closure resolved ticket', 0)
) AS seed (Citation, Question, Answer, Keywords, InsufficientInformation)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Policies AS existing WITH (UPDLOCK, HOLDLOCK)
    WHERE existing.Citation = seed.Citation
);

COMMIT TRANSACTION;
GO
