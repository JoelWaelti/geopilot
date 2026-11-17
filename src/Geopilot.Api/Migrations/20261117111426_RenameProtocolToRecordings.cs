﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <summary>
    /// Renames the execution protocol from PipelineRun to Recording (#1195). Renames only, never drop and create, so
    /// existing protocols are kept and no key or index is rebuilt: a rebuild on large protocol tables could exceed the
    /// command timeout of the migrations applied at startup. Every key, index and constraint name follows the EF
    /// convention, so the new name is the old one with PipelineRun replaced. The identity sequences keep their names.
    /// </summary>
    public partial class RenameProtocolToRecordings : Migration
    {
        private const string OldPrefix = "PipelineRun";
        private const string NewPrefix = "Recording";

        private static readonly string[] Tables =
        [
            "PipelineRuns",
            "PipelineRunFiles",
            "PipelineRunSteps",
            "PipelineRunInputs",
            "PipelineRunConditions",
            "PipelineRunArtifacts",
            "PipelineRunReactionEvents",
        ];

        private static readonly (string Table, string Column)[] Columns =
        [
            ("PipelineRunFiles", "PipelineRunId"),
            ("PipelineRunSteps", "PipelineRunId"),
            ("PipelineRunReactionEvents", "PipelineRunId"),
            ("PipelineRunInputs", "PipelineRunStepId"),
            ("PipelineRunConditions", "PipelineRunStepId"),
            ("PipelineRunArtifacts", "PipelineRunStepId"),
        ];

        // Primary, foreign and check constraints. Renaming a primary key renames its index along with it.
        private static readonly (string Table, string Constraint)[] Constraints =
        [
            ("PipelineRuns", "PK_PipelineRuns"),
            ("PipelineRuns", "FK_PipelineRuns_MachineClients_MachineClientId"),
            ("PipelineRuns", "FK_PipelineRuns_Mandates_MandateId"),
            ("PipelineRuns", "FK_PipelineRuns_Users_UserId"),
            ("PipelineRuns", "CK_PipelineRuns_Declarer"),
            ("PipelineRunFiles", "PK_PipelineRunFiles"),
            ("PipelineRunFiles", "FK_PipelineRunFiles_PipelineRuns_PipelineRunId"),
            ("PipelineRunSteps", "PK_PipelineRunSteps"),
            ("PipelineRunSteps", "FK_PipelineRunSteps_PipelineRuns_PipelineRunId"),
            ("PipelineRunInputs", "PK_PipelineRunInputs"),
            ("PipelineRunInputs", "FK_PipelineRunInputs_PipelineRunSteps_PipelineRunStepId"),
            ("PipelineRunConditions", "PK_PipelineRunConditions"),
            ("PipelineRunConditions", "FK_PipelineRunConditions_PipelineRunSteps_PipelineRunStepId"),
            ("PipelineRunArtifacts", "PK_PipelineRunArtifacts"),
            ("PipelineRunArtifacts", "FK_PipelineRunArtifacts_PipelineRunSteps_PipelineRunStepId"),
            ("PipelineRunReactionEvents", "PK_PipelineRunReactionEvents"),
            ("PipelineRunReactionEvents", "FK_PipelineRunReactionEvents_PipelineRuns_PipelineRunId"),
            ("PipelineRunReactionEvents", "FK_PipelineRunReactionEvents_Users_UserId"),
        ];

        private static readonly (string Table, string Index)[] Indexes =
        [
            ("PipelineRuns", "IX_PipelineRuns_MachineClientId"),
            ("PipelineRuns", "IX_PipelineRuns_MandateId"),
            ("PipelineRuns", "IX_PipelineRuns_UserId"),
            ("PipelineRunFiles", "IX_PipelineRunFiles_PipelineRunId"),
            ("PipelineRunSteps", "IX_PipelineRunSteps_PipelineRunId_StepId"),
            ("PipelineRunInputs", "IX_PipelineRunInputs_PipelineRunStepId"),
            ("PipelineRunConditions", "IX_PipelineRunConditions_PipelineRunStepId"),
            ("PipelineRunArtifacts", "IX_PipelineRunArtifacts_PipelineRunStepId"),
            ("PipelineRunReactionEvents", "IX_PipelineRunReactionEvents_PipelineRunId"),
            ("PipelineRunReactionEvents", "IX_PipelineRunReactionEvents_UserId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.RenameTable(name: table, newName: Renamed(table));

            RenameMembers(migrationBuilder, from: OldPrefix, to: NewPrefix);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameMembers(migrationBuilder, from: NewPrefix, to: OldPrefix);

            foreach (var table in Tables)
                migrationBuilder.RenameTable(name: Renamed(table), newName: table);
        }

        /// <summary>
        /// Renames the columns, constraints and indexes of the tables, which carry their new names at that point.
        /// </summary>
        private static void RenameMembers(MigrationBuilder migrationBuilder, string from, string to)
        {
            foreach (var (table, column) in Columns)
                migrationBuilder.RenameColumn(name: column.Replace(OldPrefix, from, StringComparison.Ordinal), table: Renamed(table), newName: column.Replace(OldPrefix, to, StringComparison.Ordinal));

            foreach (var (table, constraint) in Constraints)
                migrationBuilder.Sql($"ALTER TABLE \"{Renamed(table)}\" RENAME CONSTRAINT \"{constraint.Replace(OldPrefix, from, StringComparison.Ordinal)}\" TO \"{constraint.Replace(OldPrefix, to, StringComparison.Ordinal)}\";");

            foreach (var (table, index) in Indexes)
                migrationBuilder.RenameIndex(name: index.Replace(OldPrefix, from, StringComparison.Ordinal), table: Renamed(table), newName: index.Replace(OldPrefix, to, StringComparison.Ordinal));
        }

        private static string Renamed(string name) => name.Replace(OldPrefix, NewPrefix, StringComparison.Ordinal);
    }
}
