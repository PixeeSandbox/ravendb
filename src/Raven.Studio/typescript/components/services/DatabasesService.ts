/// <reference path="../../../typings/tsd.d.ts" />

import getDatabasesCommand from "commands/resources/getDatabasesCommand";
import saveDatabaseLockModeCommand from "commands/resources/saveDatabaseLockModeCommand";
import DatabaseLockMode = Raven.Client.ServerWide.DatabaseLockMode;
import { DatabaseSharedInfo } from "../models/databases";
import database from "models/resources/database";
import EssentialDatabaseStatistics = Raven.Client.Documents.Operations.EssentialDatabaseStatistics;
import getEssentialDatabaseStatsCommand from "commands/resources/getEssentialDatabaseStatsCommand";
import DetailedDatabaseStatistics = Raven.Client.Documents.Operations.DetailedDatabaseStatistics;
import getDatabaseDetailedStatsCommand from "commands/resources/getDatabaseDetailedStatsCommand";
import getDatabaseCommand from "commands/resources/getDatabaseCommand";
import deleteDatabaseFromNodeCommand from "commands/resources/deleteDatabaseFromNodeCommand";
import toggleDynamicNodeAssignmentCommand from "commands/database/dbGroup/toggleDynamicNodeAssignmentCommand";

export default class DatabasesService {
    async getDatabase(name: string) {
        return new getDatabaseCommand(name).execute();
    }

    async getDatabases() {
        return new getDatabasesCommand().execute();
    }

    async setLockMode(db: DatabaseSharedInfo, newLockMode: DatabaseLockMode) {
        return new saveDatabaseLockModeCommand([db], newLockMode).execute();
    }

    async getEssentialStats(db: database): Promise<EssentialDatabaseStatistics> {
        return new getEssentialDatabaseStatsCommand(db).execute();
    }

    async getDetailedStats(db: database, location: databaseLocationSpecifier): Promise<DetailedDatabaseStatistics> {
        return new getDatabaseDetailedStatsCommand(db, location).execute();
    }

    async deleteDatabaseFromNode(db: database, nodes: string[], hardDelete: boolean) {
        return new deleteDatabaseFromNodeCommand(db, nodes, hardDelete).execute();
    }

    async toggleDynamicNodeAssignment(db: database, enabled: boolean) {
        return new toggleDynamicNodeAssignmentCommand(db.name, enabled).execute();
    }
}
