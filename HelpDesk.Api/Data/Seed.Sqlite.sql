CREATE TABLE IF NOT EXISTS Policies (
    Citation TEXT NOT NULL PRIMARY KEY,
    Question TEXT NOT NULL UNIQUE,
    Answer TEXT NOT NULL,
    Keywords TEXT NOT NULL,
    InsufficientInformation INTEGER NOT NULL
);

-- Populate an empty file with the sample policies. Preserve an imported policy catalog.
WITH sample(Citation, Question, Answer, Keywords, InsufficientInformation) AS (VALUES
    ('IT Policy §4.2', 'Can I install Zoom on my work laptop?', 'The excerpt does not say whether Zoom is in the approved software catalog. If Zoom is listed, you may install it. If it is not listed, get IT approval first. This rule applies to any software installed on a work computer.', 'install installing software zoom catalog applications apps', 1),
    ('IT Policy §4.3', 'How long does it take IT to approve a software request?', 'The software approval policy gives no fixed approval timeline. IT reviews requests for software outside the approved catalog. An initial help desk reply and a software approval decision are separate.', 'approve approval approvals turnaround timeline decision timeframe software', 1),
    ('IT Policy §2.1', 'How long does it take help desk to reply to my request?', 'It typically takes 2 hours for IT to respond to service requests. This is the expected initial reply time. Resolution times vary with the issue.', 'reply replies respond response responds sla service turnaround', 0),
    ('IT Policy §3.1', 'How do I reset a forgotten password?', 'Use the self-service password reset portal and complete identity verification. If you cannot verify your identity, contact the help desk. Never send your password in a ticket.', 'reset resetting forgotten forgot password passwords', 0),
    ('IT Policy §3.2', 'How do I move MFA to a new phone?', 'If you still have your old phone, add the new device in Security Info before removing the old one. If you cannot sign in, contact IT for identity verification and MFA recovery.', 'mfa authenticator authentication factor phone mobile', 0),
    ('IT Policy §5.1', 'What should I do if the VPN will not connect?', 'Check your internet connection, reopen the company VPN client, and try signing in again. If it still fails, include the VPN error message and timestamp in an IT ticket.', 'vpn reconnect tunnel tunnels', 0),
    ('IT Policy §5.2', 'How do I get guest Wi-Fi for a visitor?', 'Connect visitors to the company guest network. A host can request a one-day access code from reception. Guest Wi-Fi has internet access only.', 'guest visitor visitors reception wifi wireless', 0),
    ('IT Policy §5.3', 'How do I connect to employee Wi-Fi?', 'On a managed laptop, select the corporate wireless network and sign in with your company account. Contact IT if the network is absent from the managed profile.', 'employee corporate managed wifi wireless network', 0),
    ('IT Policy §6.1', 'How do I report a phishing email?', 'Use the Report Phishing button in your mail client. If it is unavailable, forward the original message as an attachment to the security team. Avoid opening links or attachments.', 'phishing suspicious scam email message report', 0),
    ('IT Policy §6.2', 'What should I do if my work laptop is lost or stolen?', 'Contact the IT security desk immediately and provide the last known location and time. IT can lock the device and revoke active sessions.', 'lost stolen theft missing laptop device', 0),
    ('IT Policy §7.1', 'How do I add a shared office printer?', 'Open the managed printer portal and choose the printer by its office location and asset label. Contact IT with the label if it does not appear.', 'printer printers print printing toner', 0),
    ('IT Policy §7.2', 'How do I request equipment for a new employee?', 'The hiring manager should submit an onboarding request at least five business days before the start date. Include the employee''s role, location, start date, and required equipment.', 'onboarding hire hiring starter equipment employee', 0),
    ('IT Policy §8.1', 'How do I request access to a shared mailbox?', 'Open an access request with the mailbox address and the requested permissions. The mailbox owner must approve access before IT grants it.', 'shared mailbox mailboxes delegate delegation permissions', 0),
    ('IT Policy §8.2', 'Can IT recover a deleted file?', 'Check the recycle bin or version history first. If the file was stored in a company-managed location, submit a restore request with its name, location, and approximate deletion date.', 'restore recover deleted deletion recycle file files backup', 0),
    ('IT Policy §4.4', 'Can I postpone a required restart after an update?', 'You may postpone a standard update restart until the deadline shown in the update prompt. Save your work before that deadline. Urgent security updates can have a shorter deadline.', 'restart reboot update updates postpone patch patches', 0),
    ('IT Policy §7.3', 'How do I request a replacement for broken hardware?', 'Create a hardware ticket with the device asset tag, symptoms, and your location. IT will diagnose the issue before issuing a repair or replacement.', 'broken hardware replacement replace repair damaged asset', 0),
    ('IT Policy §6.3', 'Can I use a USB drive for company files?', 'Use company-issued encrypted USB drives for company files. A personal removable drive requires an approved exception from the security team.', 'usb removable thumb flash drive drives encrypted', 0),
    ('IT Policy §5.4', 'How do I request remote desktop access?', 'Submit an access request with the target computer, business reason, and manager approval. Use the company remote access gateway after IT grants permission.', 'remote desktop rdp gateway', 0),
    ('IT Policy §3.3', 'What do I do if my account is locked?', 'If your company account is locked after repeated sign-in attempts, wait 15 minutes before trying once more. Use the self-service unlock option or contact IT if you remain locked out.', 'lockout locked unlock account sign-in', 0),
    ('IT Policy §2.2', 'How do I reopen a closed help desk ticket?', 'Reply to the ticket confirmation or reopen the ticket in the service portal within five business days. After that, open a new ticket and reference the old ticket number.', 'reopen closed closure resolved ticket', 0)
)
INSERT INTO Policies (Citation, Question, Answer, Keywords, InsufficientInformation)
SELECT Citation, Question, Answer, Keywords, InsufficientInformation
FROM sample
WHERE NOT EXISTS (SELECT 1 FROM Policies);

CREATE TABLE IF NOT EXISTS SoftwareCatalog (
    CatalogKey TEXT NOT NULL PRIMARY KEY,
    Product TEXT NOT NULL,
    IsApproved INTEGER NOT NULL CHECK (IsApproved IN (0, 1))
);

INSERT INTO SoftwareCatalog (CatalogKey, Product, IsApproved)
VALUES
    ('zoom', 'Zoom', 1),
    ('blender', 'Blender', 0)
ON CONFLICT (CatalogKey) DO NOTHING;
