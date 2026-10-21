import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type DoseStatus = Schemas['DoseStatus'];

export type MedicineSchedule = Schemas['MedicineScheduleResponse'];

export type MedicineDoseOccurrence = Schemas['MedicineDoseOccurrence'];

export type MedicineScheduleDetails = Schemas['UpdateMedicineDetailsRequest'];

export type CreateMedicineScheduleRequest = Schemas['CreateMedicineScheduleRequest'];

export type RescheduleMedicineRequest = Schemas['RescheduleMedicineRequest'];

export type EffectArea = Schemas['EffectArea'];
export type EffectLevel = Schemas['EffectLevel'];
export type SideEffect = Schemas['SideEffect'];
export type SideEffectSeverity = Schemas['SideEffectSeverity'];
export type EffectRating = Schemas['EffectRating'];
export type SideEffectRating = Schemas['SideEffectRating'];

// One day's note. Times are "HH:mm" here; the API answers "HH:mm:ss", which normalizeEffectNote
// trims so they bind straight to app-time-select and the timeOfDay pipe.
export type MedicineEffectNote = Schemas['MedicineEffectNoteResponse'];
export type RegimenMedicine = Schemas['RegimenMedicineResponse'];

// Every field optional; saving always overwrites the whole day, as the sleep diary does.
export type MedicineEffectNoteRequest = Schemas['LogMedicineEffectNoteRequest'];

function normalizeEffectNote(note: MedicineEffectNote): MedicineEffectNote {
  return {
    ...note,
    woreOffAt: note.woreOffAt === null ? null : note.woreOffAt.slice(0, 5),
    regimen: note.regimen.map((medicine) => ({
      ...medicine,
      doses: medicine.doses.map((dose) => ({ ...dose, time: dose.time.slice(0, 5) })),
    })),
  };
}

@Injectable({ providedIn: 'root' })
export class MedicinesService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listSchedules(childId: string): Promise<MedicineSchedule[]> {
    return firstValueFrom(
      this.http.get<MedicineSchedule[]>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules`,
      ),
    ).then(sortByName);
  }

  createSchedule(
    childId: string,
    request: CreateMedicineScheduleRequest,
  ): Promise<MedicineSchedule> {
    return firstValueFrom(
      postIdempotent<MedicineSchedule>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules`,
        request,
      ),
    );
  }

  updateScheduleDetails(
    childId: string,
    medicineId: string,
    request: MedicineScheduleDetails,
  ): Promise<MedicineSchedule> {
    return firstValueFrom(
      this.http.patch<MedicineSchedule>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}/details`,
        request,
      ),
    );
  }

  rescheduleSchedule(
    childId: string,
    medicineId: string,
    request: RescheduleMedicineRequest,
  ): Promise<MedicineSchedule> {
    return firstValueFrom(
      this.http.patch<MedicineSchedule>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}/schedule`,
        request,
      ),
    );
  }

  stopSchedule(childId: string, medicineId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}`,
      ),
    );
  }

  // Leaves a medicine out of (or back in) the effect summaries
  // (docs/backend/analysis/medicine-effect-inclusion.md).
  setEffectInclusion(childId: string, medicineId: string, included: boolean): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}/effect-inclusion`,
        { included },
      ),
    );
  }

  listDoses(childId: string, from: string, to: string): Promise<MedicineDoseOccurrence[]> {
    return firstValueFrom(
      this.http.get<MedicineDoseOccurrence[]>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/doses`,
        {
          params: { from, to },
        },
      ),
    );
  }

  setDoseStatus(
    childId: string,
    medicineId: string,
    date: string,
    time: string,
    status: DoseStatus,
  ): Promise<MedicineDoseOccurrence> {
    return firstValueFrom(
      this.http.put<MedicineDoseOccurrence>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/doses/${medicineId}`,
        { status },
        { params: { date, time } },
      ),
    );
  }

  async listEffectNotes(childId: string, from: string, to: string): Promise<MedicineEffectNote[]> {
    const response = await firstValueFrom(
      this.http.get<Schemas['MedicineEffectNotesResponse']>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/effect-notes`,
        { params: { from, to } },
      ),
    );

    return response.notes.map(normalizeEffectNote);
  }

  // A PUT on the date key is idempotent by itself, so no postIdempotent.
  async logEffectNote(
    childId: string,
    date: string,
    request: MedicineEffectNoteRequest,
  ): Promise<MedicineEffectNote> {
    const note = await firstValueFrom(
      this.http.put<MedicineEffectNote>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/effect-notes/${date}`,
        request,
      ),
    );

    return normalizeEffectNote(note);
  }

  clearEffectNote(childId: string, date: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/effect-notes/${date}`,
      ),
    );
  }

  // Sharing is always a guardian-side action (only a guardian, via CheckManage, can decide to
  // share or unshare a child's medicine schedules) -- mirrors MealplansService's equivalent.
  shareWithGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share/${groupId}`,
        {},
      ),
    );
  }

  unshareFromGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share/${groupId}`,
      ),
    );
  }

  // 200 with the group, or 204 (an empty body, so null) when it isn't shared.
  getSharedGroup(childId: string): Promise<{ groupId: string; groupName: string } | null> {
    return firstValueFrom(
      this.http.get<{ groupId: string; groupName: string } | null>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share`,
      ),
    );
  }
}
