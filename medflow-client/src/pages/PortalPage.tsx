import { useState } from 'react'
import {
  usePortalMe, usePortalAppointments, usePortalPrescriptions, usePortalInvoices,
  usePortalAttachments, usePortalNotes, usePortalSlots, useBookAppointment,
} from '@/hooks/queries'
import { portalApi } from '@/api/services'
import { Badge, EmptyState, PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import { CalendarPlus, CalendarDays, Pill, CreditCard, FileText, StickyNote, Download } from 'lucide-react'
import toast from 'react-hot-toast'

type Q<T> = { data?: T[]; isLoading: boolean; isError: boolean }

function Section<T>({ icon, title, query, empty, children }: {
  icon: React.ReactNode; title: string; query: Q<T>; empty: string
  children: (items: T[]) => React.ReactNode
}) {
  return (
    <section className="card p-5">
      <h2 className="flex items-center gap-2 text-base font-semibold text-gray-900 mb-4">{icon}{title}</h2>
      {query.isLoading ? (
        <div className="flex justify-center py-6"><Spinner className="w-6 h-6" /></div>
      ) : query.isError ? (
        <p className="text-sm text-red-500">Could not load this section. Please try again later.</p>
      ) : !query.data || query.data.length === 0 ? (
        <EmptyState title={empty} />
      ) : (
        children(query.data)
      )}
    </section>
  )
}

function BookingSection() {
  const [date, setDate] = useState('')
  const [reason, setReason] = useState('')
  const slots = usePortalSlots(date)
  const book = useBookAppointment()
  const today = new Date().toISOString().slice(0, 10)

  return (
    <section className="card p-5">
      <h2 className="flex items-center gap-2 text-base font-semibold text-gray-900 mb-4">
        <CalendarPlus size={18} className="text-primary-600" />Book an appointment
      </h2>
      <div className="flex flex-wrap gap-3 mb-4">
        <div>
          <label className="label" htmlFor="booking-date">Date</label>
          <input id="booking-date" className="input" type="date" min={today} value={date}
            onChange={e => setDate(e.target.value)} />
        </div>
        <div className="flex-1 min-w-[200px]">
          <label className="label" htmlFor="booking-reason">Reason (optional)</label>
          <input id="booking-reason" className="input" maxLength={500} value={reason}
            onChange={e => setReason(e.target.value)} placeholder="Reason for visit..." />
        </div>
      </div>
      {!date ? (
        <p className="text-sm text-gray-500">Choose a date to see open times (UTC).</p>
      ) : slots.isLoading ? (
        <div className="flex justify-center py-4"><Spinner className="w-5 h-5" /></div>
      ) : slots.isError ? (
        <p className="text-sm text-red-500">Could not load open times. Please try again.</p>
      ) : !slots.data || slots.data.length === 0 ? (
        <EmptyState title="No open times on this date" />
      ) : (
        <div className="flex flex-wrap gap-2">
          {slots.data.map(s => (
            <button key={s.startsAt} className="btn-secondary" disabled={book.isPending}
              onClick={() => book.mutate({ startsAt: s.startsAt, reason: reason.trim() || undefined })}>
              {new Date(s.startsAt).toISOString().slice(11, 16)}
            </button>
          ))}
        </div>
      )}
    </section>
  )
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function PortalPage() {
  const me = usePortalMe()
  const appointments = usePortalAppointments()
  const prescriptions = usePortalPrescriptions()
  const invoices = usePortalInvoices()
  const attachments = usePortalAttachments()
  const notes = usePortalNotes()
  const [downloading, setDownloading] = useState<number | null>(null)

  const download = async (id: number, fileName: string) => {
    setDownloading(id)
    try { await portalApi.downloadAttachment(id, fileName) }
    catch { toast.error('Could not download this file') }
    finally { setDownloading(null) }
  }

  if (me.isLoading) return <PageSpinner />
  if (me.isError) {
    return (
      <div className="card p-5 sm:p-8 text-center">
        <h1 className="text-lg font-semibold text-gray-900">Portal unavailable</h1>
        <p className="text-sm text-gray-500 mt-2">
          Your portal access is not available right now. Please contact your doctor's office.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Hello, {me.data?.firstName}</h1>
        <p className="text-sm text-gray-500 mt-1">Your records from {me.data?.doctorName}</p>
      </div>

      <BookingSection />

      <Section icon={<CalendarDays size={18} className="text-primary-600" />} title="Upcoming appointments"
        query={appointments} empty="No upcoming appointments">
        {items => (
          <ul className="divide-y divide-gray-100">
            {items.map(a => (
              <li key={a.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900">{fmt.dateTime(a.scheduledAt)}</p>
                  <p className="text-sm text-gray-500">
                    {[a.reason, a.location, `${a.durationMinutes} min`].filter(Boolean).join(' · ')}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge status={a.type} /><Badge status={a.status} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section icon={<Pill size={18} className="text-primary-600" />} title="Prescriptions"
        query={prescriptions} empty="No prescriptions">
        {items => (
          <ul className="divide-y divide-gray-100">
            {items.map(p => (
              <li key={p.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="font-medium text-gray-900">{p.drugName} <span className="text-gray-500 font-normal">{p.dosage}</span></p>
                  <p className="text-sm text-gray-500">
                    {p.frequency} · Issued {fmt.date(p.issuedDate)} · Expires {fmt.date(p.expiryDate)} · {p.refillsRemaining} refill{p.refillsRemaining === 1 ? '' : 's'} left
                  </p>
                  {p.instructions && <p className="text-sm text-gray-500 mt-0.5">{p.instructions}</p>}
                </div>
                <Badge status={p.status} />
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section icon={<CreditCard size={18} className="text-primary-600" />} title="Invoices"
        query={invoices} empty="No invoices">
        {items => (
          <ul className="divide-y divide-gray-100">
            {items.map(i => (
              <li key={i.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="font-medium text-gray-900">{i.invoiceNumber} <span className="text-gray-500 font-normal">· {i.serviceDescription}</span></p>
                  <p className="text-sm text-gray-500">
                    {fmt.date(i.invoiceDate)}{i.dueDate ? ` · Due ${fmt.date(i.dueDate)}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="font-semibold text-gray-900">{fmt.currency(i.amount)}</span>
                  <Badge status={i.status} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section icon={<FileText size={18} className="text-primary-600" />} title="Documents shared with you"
        query={attachments} empty="Nothing has been shared with you yet">
        {items => (
          <ul className="divide-y divide-gray-100">
            {items.map(f => (
              <li key={f.id} className="py-3 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900 truncate">{f.fileName}</p>
                  <p className="text-sm text-gray-500">
                    {[f.category, formatSize(f.fileSize), fmt.date(f.createdAt)].filter(Boolean).join(' · ')}
                  </p>
                  {f.description && <p className="text-sm text-gray-500">{f.description}</p>}
                </div>
                <button className="btn-secondary flex-shrink-0 inline-flex items-center gap-1.5"
                  onClick={() => download(f.id, f.fileName)} disabled={downloading === f.id}>
                  {downloading === f.id ? <Spinner className="w-4 h-4" /> : <Download size={15} />} Download
                </button>
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section icon={<StickyNote size={18} className="text-primary-600" />} title="Notes from your doctor"
        query={notes} empty="No notes have been shared with you yet">
        {items => (
          <ul className="space-y-3">
            {items.map(n => (
              <li key={n.id} className="rounded-lg bg-gray-50 p-4">
                <p className="text-xs text-gray-500 mb-1">
                  {n.doctorName} · {fmt.date(n.noteDate)}{n.visitType ? ` · ${n.visitType}` : ''}
                </p>
                <p className="text-sm text-gray-800 whitespace-pre-wrap">{n.content}</p>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}
