-- The split runtime uses two databases on the shared MariaDB: the state DB (controller +
-- compute API) and identity's own DB. The mariadb image only auto-creates one
-- (MARIADB_DATABASE), so create both (empty) here. The schemas are created out of band by the
-- one-shot dbsetup / identity-dbsetup steps (the create-db command), not migrated at startup.
CREATE DATABASE IF NOT EXISTS `eryph`;
CREATE DATABASE IF NOT EXISTS `eryph_identity`;
