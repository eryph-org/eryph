-- The split runtime uses two databases on the shared MariaDB: the state DB (controller +
-- compute API) and identity's own DB. The mariadb image only auto-creates one
-- (MARIADB_DATABASE), so create both here; each component migrates its own schema at startup.
CREATE DATABASE IF NOT EXISTS `eryph`;
CREATE DATABASE IF NOT EXISTS `eryph_identity`;
