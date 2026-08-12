-- ============================================================================
-- 006_sp_addorderlegend_createdat_morocco.sql
-- sp_AddOrderLegend stamped CreatedAt with GETDATE(), which is UTC on Azure SQL
-- (= heure Maroc - 1h). Screens showing CreatedAt next to DateAffectation (now
-- correct via trigger 004) displayed the -1h skew. This re-creates the proc
-- identical to prod except CreatedAt now uses the same Morocco convention as
-- ParkingAt / PabEntryAt / the DateAffectation trigger.
-- Source of truth for the previous definition: prod OBJECT_DEFINITION captured
-- 2026-08-12 (only change: GETDATE() -> AT TIME ZONE 'Morocco Standard Time').
-- Safe to run multiple times (CREATE OR ALTER).
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_AddOrderLegend]
(
      @UserId             NVARCHAR(100),
      @Matricule          NVARCHAR(100)       = NULL,
      @ClientName         NVARCHAR(255),
      @Chantier           NVARCHAR(255),
      @RFIDCard           NVARCHAR(100)       = NULL,

      @Produit1           INT                 = NULL,
      @Produit2           INT                 = NULL,

      @Quantite1          FLOAT               = NULL,
      @Quantite2          FLOAT               = NULL,
      @BonDeCommande      NVARCHAR(100)       = NULL,

      @CodeSapProduit1    NVARCHAR(50)        = NULL,
      @CodeSapProduit2    NVARCHAR(50)        = NULL,
      @CodeSapClient      NVARCHAR(50)        = NULL,
      @CodeSapChantier    NVARCHAR(50)        = NULL,
      @CodeSapCommande    NVARCHAR(50)        = NULL,

      @OrderId            INT                 = NULL,
      @CommercialOrderId  INT                 = NULL,
      @TypeCamion         NVARCHAR(50)        = NULL,
      @NombrePlombs       INT                 = NULL,
      @TypeProduit        NVARCHAR(100)       = NULL,
      @Status             NVARCHAR(30)        = NULL,

      @ChauffeurName        NVARCHAR(255)     = NULL,
      @CodeTransporteurSap  NVARCHAR(50)      = NULL,
      @TransporteurName     NVARCHAR(255)     = NULL,
      @PermisDeConduite     NVARCHAR(100)     = NULL,
      @NombreSacs           INT               = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        ---------------------------------------------------
        -- Resolve product names from EcareCiments
        ---------------------------------------------------
        DECLARE @Produit1Name NVARCHAR(255) = NULL;
        DECLARE @Produit2Name NVARCHAR(255) = NULL;

        IF @Produit1 IS NOT NULL
        BEGIN
            SELECT @Produit1Name = Name
            FROM EcareCiments
            WHERE Id = @Produit1;
        END

        IF @Produit2 IS NOT NULL
        BEGIN
            SELECT @Produit2Name = Name
            FROM EcareCiments
            WHERE Id = @Produit2;
        END

        ---------------------------------------------------
        -- Insert into Ecare_Order_Legend
        ---------------------------------------------------
        DECLARE @NewId INT;

        INSERT INTO Ecare_Order_Legend
        (
            OrderId,
            CommercialOrderId,
            ClientName,
            Chantier,
            Matricule,
            RFIDCard,
            TypeCamion,
            NombrePlombs,
            ChauffeurName,
            CodeTransporteurSap,
            TransporteurName,
            PermisDeConduite,
            Produit1,
            Quantite1,
            Produit2,
            Quantite2,
            TypeProduit,
            CodeClientSAP,
            BonDeCommande,
            UserId,
            CodeSapChantier,
            CodeSapClient,
            CodeSapCommande,
            CodeSapProduit1,
            CodeSapProduit2,
            Status,
            Step,
            CreatedAt,
            SacNumber
        )
        VALUES
        (
            @OrderId,
            @CommercialOrderId,
            @ClientName,
            @Chantier,
            @Matricule,
            @RFIDCard,
            @TypeCamion,
            @NombrePlombs,
            @ChauffeurName,
            @CodeTransporteurSap,
            @TransporteurName,
            @PermisDeConduite,
            @Produit1Name,
            @Quantite1,
            @Produit2Name,
            @Quantite2,
            @TypeProduit,
            @CodeSapClient,              -- CodeClientSAP
            @BonDeCommande,
            @UserId,
            @CodeSapChantier,
            @CodeSapClient,
            @CodeSapCommande,
            @CodeSapProduit1,
            @CodeSapProduit2,
            ISNULL(@Status, 'Crée'),
            0,                           -- Step initial
            CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time' AS DATETIME),  -- CreatedAt (heure Maroc)
            @NombreSacs
        );

        SET @NewId = SCOPE_IDENTITY();

        COMMIT TRAN;

        -- renvoyer l'Id pour ExecuteScalar<int>
        SELECT @NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;

        THROW;
    END CATCH
END;
GO
