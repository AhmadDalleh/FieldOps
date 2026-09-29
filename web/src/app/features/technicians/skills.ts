import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { problemMessage } from '../../core/problem';
import { Skill, TechniciansApi } from './technicians.api';

@Component({
  selector: 'app-skills',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatListModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Skills</h1>
    <form class="add" (ngSubmit)="add()">
      <mat-form-field appearance="outline">
        <mat-label>New skill</mat-label>
        <input matInput name="name" [(ngModel)]="newName" />
      </mat-form-field>
      <button mat-flat-button type="submit" [disabled]="!newName().trim() || busy()">Add</button>
    </form>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    <mat-list class="list">
      @for (skill of skills.value() ?? []; track skill.id) {
        <mat-list-item>
          @if (editingId() === skill.id) {
            <div class="edit">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput aria-label="Skill name" [(ngModel)]="editName" (keydown.enter)="rename(skill)" (keydown.escape)="editingId.set(null)" />
              </mat-form-field>
              <button mat-button (click)="rename(skill)" [disabled]="!editName().trim() || busy()">Save</button>
              <button mat-button (click)="editingId.set(null)">Cancel</button>
            </div>
          } @else {
            <div class="edit">
              <span class="name">{{ skill.name }}</span>
              <button mat-icon-button aria-label="Rename skill" (click)="startEdit(skill)"><mat-icon>edit</mat-icon></button>
            </div>
          }
        </mat-list-item>
      } @empty {
        <p class="muted">No skills yet.</p>
      }
    </mat-list>
  `,
  styles: `
    .add { display: flex; gap: 12px; align-items: baseline; }
    .list { max-width: 520px; }
    .edit { display: flex; gap: 8px; align-items: center; }
    .name { min-width: 240px; }
    .error { color: var(--mat-sys-error); }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Skills {
  private readonly api = inject(TechniciansApi);
  protected readonly skills = rxResource({ stream: () => this.api.skills() });
  protected readonly newName = signal('');
  protected readonly editName = signal('');
  protected readonly editingId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected startEdit(skill: Skill): void {
    this.editName.set(skill.name);
    this.editingId.set(skill.id);
    this.error.set(null);
  }

  protected add(): void {
    this.run(this.api.createSkill(this.newName().trim()), () => this.newName.set(''));
  }

  protected rename(skill: Skill): void {
    this.run(this.api.renameSkill(skill.id, this.editName().trim()), () => this.editingId.set(null));
  }

  private run(request: ReturnType<TechniciansApi['createSkill']>, done: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        done();
        this.busy.set(false);
        this.skills.reload();
      },
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
