-- Dev ortamı seed verisi
-- Uygulama ilk kez ayağa kalktıktan sonra çalıştırılır

USE auth_db;
GO

-- Admin kullanıcı (şifre: Admin1234! — BCrypt work factor 12)
IF NOT EXISTS (SELECT 1 FROM users WHERE username = 'admin@qrpay.dev')
BEGIN
    INSERT INTO users (id, username, password_hash, role, is_active)
    VALUES (NEWID(), 'admin@qrpay.dev',
            '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewkKHpSf9UcHu3M2',
            'ADMIN', 1);
END
GO

USE onboarding_db;
GO

DECLARE @MerchantId UNIQUEIDENTIFIER = NEWID();
DECLARE @BranchId   UNIQUEIDENTIFIER = NEWID();

-- Test merchant
IF NOT EXISTS (SELECT 1 FROM merchants WHERE tax_number = '1234567890')
BEGIN
    INSERT INTO merchants (id, title, tax_number, iban, mcc, status, is_active)
    VALUES (@MerchantId, 'Test Bakkal', '1234567890',
            'TR000000000000000000000001', '5411', 'APPROVED', 1);

    INSERT INTO branches (id, merchant_id, name, address, is_active)
    VALUES (@BranchId, @MerchantId, 'Ana Şube', 'Test Cd. No:1 İstanbul', 1);

    -- Test terminal (HMAC secret: dev-only-not-for-production)
    INSERT INTO terminals (id, merchant_id, branch_id, secret_key, is_active)
    VALUES ('TID001', @MerchantId, @BranchId,
            'ZGV2LW9ubHktbm90LWZvci1wcm9kdWN0aW9u', 1);
END
GO
