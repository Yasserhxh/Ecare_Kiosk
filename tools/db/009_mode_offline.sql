-- 009 : Mode Offline — flags sur Ecare_Order_Legend
-- A exécuter AVANT tout déploiement des tasks 2+.
IF COL_LENGTH('dbo.Ecare_Order_Legend', 'IsOffline') IS NULL
BEGIN
    ALTER TABLE dbo.Ecare_Order_Legend ADD
        IsOffline        bit NOT NULL CONSTRAINT DF_EOL_IsOffline DEFAULT(0),
        OfflineStatus    nvarchar(20)   NULL,
        OfflineSyncError nvarchar(1000) NULL,
        OfflineCreatedAt datetime NULL,
        OfflineSyncedAt  datetime NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EOL_OfflineStatus')
    CREATE INDEX IX_EOL_OfflineStatus ON dbo.Ecare_Order_Legend(OfflineStatus)
        WHERE OfflineStatus IS NOT NULL;
