import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// DoseStatus values match the backend's DoseStatus enum ordinals (no string enum converter is
// registered server-side): 0 = Pending, 1 = Taken, 2 = Skipped.
export type DoseStatus = 0 | 1 | 2;

export interface MedicineSchedule {
  id: string;
  childId: string;
  name: string;
  dosage: string;
  icon: string;
  color: string;
  times: string[];
  startDate: string;
  endDate: string | null;
  isStopped: boolean;
  createdBy: string;
  lastModifiedBy: string;
}

export interface MedicineDoseOccurrence {
  medicineId: string;
  name: string;
  dosage: string;
  icon: string;
  color: string;
  date: string;
  time: string;
  status: DoseStatus;
}

export interface MedicineScheduleDetails {
  name: string;
  dosage: string;
  icon: string;
  color: string;
}

export interface CreateMedicineScheduleRequest extends MedicineScheduleDetails {
  times: string[];
  startDate: string;
  endDate?: string | null;
}

export interface RescheduleMedicineRequest {
  times: string[];
  startDate: string;
  endDate?: string | null;
}

@Injectable({ providedIn: 'root' })
export class MedicinesService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listSchedules(childId: string): Promise<MedicineSchedule[]> {
    return firstValueFrom(
      this.http.get<MedicineSchedule[]>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules`)
    );
  }

  createSchedule(childId: string, request: CreateMedicineScheduleRequest): Promise<MedicineSchedule> {
    return firstValueFrom(
      this.http.post<MedicineSchedule>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules`, request)
    );
  }

  updateScheduleDetails(childId: string, medicineId: string, request: MedicineScheduleDetails): Promise<MedicineSchedule> {
    return firstValueFrom(
      this.http.patch<MedicineSchedule>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}/details`,
        request
      )
    );
  }

  rescheduleSchedule(childId: string, medicineId: string, request: RescheduleMedicineRequest): Promise<MedicineSchedule> {
    return firstValueFrom(
      this.http.patch<MedicineSchedule>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}/schedule`,
        request
      )
    );
  }

  stopSchedule(childId: string, medicineId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/schedules/${medicineId}`)
    );
  }

  listDoses(childId: string, from: string, to: string): Promise<MedicineDoseOccurrence[]> {
    return firstValueFrom(
      this.http.get<MedicineDoseOccurrence[]>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/doses`, {
        params: { from, to }
      })
    );
  }

  setDoseStatus(
    childId: string,
    medicineId: string,
    date: string,
    time: string,
    status: DoseStatus
  ): Promise<MedicineDoseOccurrence> {
    return firstValueFrom(
      this.http.put<MedicineDoseOccurrence>(
        `${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/doses/${medicineId}`,
        { status },
        { params: { date, time } }
      )
    );
  }

  // Sharing is always a guardian-side action (only a guardian, via CheckManage, can decide to
  // share or unshare a child's medicine schedules) -- mirrors MealplansService's equivalent.
  shareWithGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share/${groupId}`, {}));
  }

  unshareFromGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share/${groupId}`)
    );
  }

  async getSharedGroupId(childId: string): Promise<string | null> {
    const response = await firstValueFrom(
      this.http.get<{ groupId: string | null }>(`${this.runtimeConfig.apiBaseUrl}/medicines/children/${childId}/group-share`)
    );
    return response.groupId;
  }
}
