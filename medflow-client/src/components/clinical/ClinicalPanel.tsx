import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Pencil, Plus, Trash2, AlertTriangle } from 'lucide-react'
import {
  usePatientClinical, useSaveAllergy, useDeleteAllergy, useSaveProblem, useDeleteProblem,
  useSaveMedication, useDeleteMedication,
} from '@/hooks/queries'
import { Spinner } from '@/components/ui'
import { displayEnum } from '@/utils/format'
import type { AllergyDto, ProblemDto, MedicationDto } from '@/types'

const ICD10 = /^\s*[A-Za-z][0-9][A-Za-z0-9](\.[A-Za-z0-9]{1,4})?\s*$/
const optional = (max: number) => z.string().max(max, `Max ${max} characters`).optional()

const allergySchema = z.object({
  substance: z.string().trim().min(1, 'Required').max(200, 'Max 200 characters'),
  reaction: optional(500),
  severity: z.enum(['Mild', 'Moderate', 'Severe', 'LifeThreatening']),
})
const problemSchema = z.object({
  description: z.string().trim().min(1, 'Required').max(200, 'Max 200 characters'),
  icd10Code: z.string().regex(ICD10, 'Enter a valid ICD-10 code, e.g. E11.9'),
  status: z.enum(['Active', 'Resolved']),
  onsetDate: z.string().optional(),
})
const medicationSchema = z.object({
  name: z.string().trim().min(1, 'Required').max(200, 'Max 200 characters'),
  dosage: optional(100),
  frequency: optional(100),
  notes: optional(500),
})

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="label">{label}</label>
      {children}
      {error && <p className="text-red-500 text-xs mt-1">{error}</p>}
    </div>
  )
}

function FormActions({ pending, onCancel }: { pending: boolean; onCancel: () => void }) {
  return (
    <div className="flex justify-end gap-2 pt-1">
      <button type="button" className="btn-ghost" onClick={onCancel}>Cancel</button>
      <button type="submit" className="btn-primary" disabled={pending}>
        {pending ? <Spinner className="w-4 h-4" /> : 'Save'}
      </button>
    </div>
  )
}

// Empty optional text is sent as undefined so the server stores null
const blank = (s?: string) => (s && s.trim() ? s.trim() : undefined)

function AllergyForm({ patientId, item, onDone }: { patientId: number; item?: AllergyDto; onDone: () => void }) {
  const save = useSaveAllergy(patientId)
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof allergySchema>>({
    resolver: zodResolver(allergySchema),
    defaultValues: { substance: item?.substance ?? '', reaction: item?.reaction ?? '', severity: item?.severity ?? 'Moderate' },
  })
  return (
    <form className="space-y-3 border border-border rounded-xl p-3 bg-gray-50"
      onSubmit={handleSubmit(d => save.mutate({ id: item?.id, data: { ...d, reaction: blank(d.reaction) } }, { onSuccess: onDone }))}>
      <Field label="Substance" error={errors.substance?.message}><input className="input" {...register('substance')} /></Field>
      <Field label="Reaction" error={errors.reaction?.message}><input className="input" {...register('reaction')} /></Field>
      <Field label="Severity" error={errors.severity?.message}>
        <select className="input" {...register('severity')}>
          {['Mild', 'Moderate', 'Severe', 'LifeThreatening'].map(s => <option key={s} value={s}>{displayEnum(s)}</option>)}
        </select>
      </Field>
      <FormActions pending={save.isPending} onCancel={onDone} />
    </form>
  )
}

function ProblemForm({ patientId, item, onDone }: { patientId: number; item?: ProblemDto; onDone: () => void }) {
  const save = useSaveProblem(patientId)
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof problemSchema>>({
    resolver: zodResolver(problemSchema),
    defaultValues: { description: item?.description ?? '', icd10Code: item?.icd10Code ?? '', status: item?.status ?? 'Active', onsetDate: item?.onsetDate ?? '' },
  })
  return (
    <form className="space-y-3 border border-border rounded-xl p-3 bg-gray-50"
      onSubmit={handleSubmit(d => save.mutate({ id: item?.id, data: { ...d, icd10Code: d.icd10Code.trim().toUpperCase(), onsetDate: blank(d.onsetDate) } }, { onSuccess: onDone }))}>
      <Field label="Problem" error={errors.description?.message}><input className="input" {...register('description')} /></Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="ICD-10 code" error={errors.icd10Code?.message}><input className="input" placeholder="E11.9" {...register('icd10Code')} /></Field>
        <Field label="Status" error={errors.status?.message}>
          <select className="input" {...register('status')}>
            <option value="Active">Active</option>
            <option value="Resolved">Resolved</option>
          </select>
        </Field>
      </div>
      <Field label="Onset date (optional)" error={errors.onsetDate?.message}><input className="input" type="date" {...register('onsetDate')} /></Field>
      <FormActions pending={save.isPending} onCancel={onDone} />
    </form>
  )
}

function MedicationForm({ patientId, item, onDone }: { patientId: number; item?: MedicationDto; onDone: () => void }) {
  const save = useSaveMedication(patientId)
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof medicationSchema>>({
    resolver: zodResolver(medicationSchema),
    defaultValues: { name: item?.name ?? '', dosage: item?.dosage ?? '', frequency: item?.frequency ?? '', notes: item?.notes ?? '' },
  })
  return (
    <form className="space-y-3 border border-border rounded-xl p-3 bg-gray-50"
      onSubmit={handleSubmit(d => save.mutate({ id: item?.id, data: { name: d.name, dosage: blank(d.dosage), frequency: blank(d.frequency), notes: blank(d.notes) } }, { onSuccess: onDone }))}>
      <Field label="Medication" error={errors.name?.message}><input className="input" {...register('name')} /></Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Dosage" error={errors.dosage?.message}><input className="input" {...register('dosage')} /></Field>
        <Field label="Frequency" error={errors.frequency?.message}><input className="input" {...register('frequency')} /></Field>
      </div>
      <Field label="Notes" error={errors.notes?.message}><input className="input" {...register('notes')} /></Field>
      <FormActions pending={save.isPending} onCancel={onDone} />
    </form>
  )
}

function Section({ title, count, onAdd, children }: { title: string; count: number; onAdd: () => void; children: React.ReactNode }) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <h4 className="font-bold text-gray-800 text-sm">{title} <span className="text-gray-400 font-normal">({count})</span></h4>
        <button type="button" className="btn-ghost p-1.5" aria-label={`Add ${title}`} onClick={onAdd}><Plus size={14} /></button>
      </div>
      {children}
    </div>
  )
}

function RowActions({ onEdit, onRemove }: { onEdit: () => void; onRemove: () => void }) {
  return (
    <div className="flex gap-1 flex-shrink-0">
      <button type="button" className="btn-ghost p-1" aria-label="Edit" onClick={onEdit}><Pencil size={13} className="text-gray-400" /></button>
      <button type="button" className="btn-ghost p-1" aria-label="Remove" onClick={onRemove}><Trash2 size={13} className="text-gray-400" /></button>
    </div>
  )
}

const None = ({ text }: { text: string }) => <p className="text-sm text-gray-400">{text}</p>

type Editing = { kind: 'allergy' | 'problem' | 'medication'; id: number | 'new' } | null

/** Prominent allergy / problem / medication summary with inline editing. Doctor-facing only. */
export function ClinicalPanel({ patientId, legacyAllergies }: { patientId: number; legacyAllergies?: string | null }) {
  const { data, isLoading, isError } = usePatientClinical(patientId)
  const delAllergy = useDeleteAllergy(patientId)
  const delProblem = useDeleteProblem(patientId)
  const delMed = useDeleteMedication(patientId)
  const [editing, setEditing] = useState<Editing>(null)
  const [showResolved, setShowResolved] = useState(false)
  const done = () => setEditing(null)
  const is = (kind: NonNullable<Editing>['kind'], id: number | 'new') => editing?.kind === kind && editing.id === id

  if (isLoading) return <div className="card p-5"><Spinner className="w-5 h-5" /></div>
  if (isError || !data) return <div className="card p-5 text-sm text-red-600">Could not load allergies, problems and medications.</div>

  const activeProblems = data.problems.filter(p => p.status === 'Active')
  const resolved = data.problems.filter(p => p.status === 'Resolved')
  const shownProblems = showResolved ? data.problems : activeProblems
  const hasSevere = data.allergies.some(a => a.severity === 'Severe' || a.severity === 'LifeThreatening')

  return (
    <div className={`card p-5 grid grid-cols-3 gap-6 border-l-4 ${hasSevere ? 'border-l-red-500' : 'border-l-primary-600'}`}
      aria-label="Clinical summary">
      <Section title="Allergies" count={data.allergies.length} onAdd={() => setEditing({ kind: 'allergy', id: 'new' })}>
        {is('allergy', 'new') && <AllergyForm patientId={patientId} onDone={done} />}
        {data.allergies.length === 0 && !is('allergy', 'new') && <None text="No allergies recorded" />}
        {data.allergies.map(a => is('allergy', a.id)
          ? <AllergyForm key={a.id} patientId={patientId} item={a} onDone={done} />
          : (
            <div key={a.id} className="flex items-start justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-gray-800 flex items-center gap-1">
                  {(a.severity === 'Severe' || a.severity === 'LifeThreatening') && <AlertTriangle size={13} className="text-red-600" />}
                  {a.substance}
                  <span className={`badge ${a.severity === 'Mild' || a.severity === 'Moderate' ? 'bg-amber-50 text-amber-700' : 'bg-red-50 text-red-700'}`}>{displayEnum(a.severity)}</span>
                </p>
                {a.reaction && <p className="text-xs text-gray-500">{a.reaction}</p>}
              </div>
              <RowActions onEdit={() => setEditing({ kind: 'allergy', id: a.id })}
                onRemove={() => { if (confirm(`Remove allergy "${a.substance}"?`)) delAllergy.mutate(a.id) }} />
            </div>
          ))}
        {legacyAllergies?.trim() && (
          <p className="text-xs text-gray-500 border-t border-border pt-2">
            <span className="font-semibold">Legacy allergy notes:</span> {legacyAllergies}
          </p>
        )}
      </Section>

      <Section title="Active problems" count={activeProblems.length} onAdd={() => setEditing({ kind: 'problem', id: 'new' })}>
        {is('problem', 'new') && <ProblemForm patientId={patientId} onDone={done} />}
        {shownProblems.length === 0 && !is('problem', 'new') && <None text="No active problems recorded" />}
        {shownProblems.map(p => is('problem', p.id)
          ? <ProblemForm key={p.id} patientId={patientId} item={p} onDone={done} />
          : (
            <div key={p.id} className="flex items-start justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-gray-800">
                  {p.description} <span className="badge bg-blue-50 text-blue-700">{p.icd10Code}</span>
                  {p.status === 'Resolved' && <span className="badge bg-gray-100 text-gray-600 ml-1">Resolved</span>}
                </p>
                {p.onsetDate && <p className="text-xs text-gray-500">Since {p.onsetDate}</p>}
              </div>
              <RowActions onEdit={() => setEditing({ kind: 'problem', id: p.id })}
                onRemove={() => { if (confirm(`Remove problem "${p.description}"?`)) delProblem.mutate(p.id) }} />
            </div>
          ))}
        {resolved.length > 0 && (
          <button type="button" className="text-xs text-primary-600" onClick={() => setShowResolved(v => !v)}>
            {showResolved ? 'Hide resolved' : `Show resolved (${resolved.length})`}
          </button>
        )}
      </Section>

      <Section title="Current medications" count={data.medications.length} onAdd={() => setEditing({ kind: 'medication', id: 'new' })}>
        {is('medication', 'new') && <MedicationForm patientId={patientId} onDone={done} />}
        {data.medications.length === 0 && !is('medication', 'new') && <None text="No medications recorded" />}
        {data.medications.map(m => is('medication', m.id)
          ? <MedicationForm key={m.id} patientId={patientId} item={m} onDone={done} />
          : (
            <div key={m.id} className="flex items-start justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-gray-800">{m.name}</p>
                <p className="text-xs text-gray-500">{[m.dosage, m.frequency].filter(Boolean).join(' · ') || '—'}</p>
                {m.notes && <p className="text-xs text-gray-400">{m.notes}</p>}
              </div>
              <RowActions onEdit={() => setEditing({ kind: 'medication', id: m.id })}
                onRemove={() => { if (confirm(`Remove medication "${m.name}"?`)) delMed.mutate(m.id) }} />
            </div>
          ))}
      </Section>
    </div>
  )
}
