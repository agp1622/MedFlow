import { useState, useRef, useCallback } from 'react'
import { usePatientAttachments, useUploadAttachment, useDeleteAttachment } from '@/hooks/queries'
import { attachmentsApi } from '@/api/services'
import { PageSpinner, EmptyState, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import {
  Upload, X, Trash2, Download, Eye, FileText, Image, Film, File,
  CloudUpload, FolderOpen,
} from 'lucide-react'
import type { PatientAttachmentDto } from '@/types'

const CATEGORIES = ['Test Result', 'Photo', 'Video', 'Document', 'Other'] as const

const MAX_SIZE = 50 * 1024 * 1024 // 50 MB

const ALLOWED_TYPES = new Set([
  'image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp', 'image/svg+xml',
  'video/mp4', 'video/webm', 'video/quicktime', 'video/x-msvideo',
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
])

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

function getFileIcon(contentType: string) {
  if (contentType.startsWith('image/')) return <Image size={20} className="text-violet-500" />
  if (contentType.startsWith('video/')) return <Film size={20} className="text-rose-500" />
  if (contentType === 'application/pdf') return <FileText size={20} className="text-red-500" />
  return <File size={20} className="text-gray-500" />
}

function getFileIconLarge(contentType: string) {
  if (contentType.startsWith('image/')) return <Image size={40} className="text-violet-400" />
  if (contentType.startsWith('video/')) return <Film size={40} className="text-rose-400" />
  if (contentType === 'application/pdf') return <FileText size={40} className="text-red-400" />
  return <File size={40} className="text-gray-400" />
}

function getCategoryColor(cat?: string) {
  switch (cat) {
    case 'Test Result': return 'bg-blue-50 text-blue-600'
    case 'Photo': return 'bg-violet-50 text-violet-600'
    case 'Video': return 'bg-rose-50 text-rose-600'
    case 'Document': return 'bg-amber-50 text-amber-600'
    default: return 'bg-gray-100 text-gray-600'
  }
}

function canPreview(contentType: string) {
  return (
    contentType.startsWith('image/') ||
    contentType.startsWith('video/') ||
    contentType === 'application/pdf'
  )
}

// ── Main Tab ──────────────────────────────────────────────────────────────────
export function AttachmentsTab({ patientId }: { patientId: number }) {
  const { data, isLoading } = usePatientAttachments(patientId)
  const uploadMutation = useUploadAttachment()
  const deleteMutation = useDeleteAttachment()

  const [dragging, setDragging] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const [previewFile, setPreviewFile] = useState<PatientAttachmentDto | null>(null)
  const [category, setCategory] = useState('')
  const [description, setDescription] = useState('')
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setDragging(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setDragging(false)
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setDragging(false)
    const files = Array.from(e.dataTransfer.files).filter(f => ALLOWED_TYPES.has(f.type) && f.size <= MAX_SIZE)
    if (files.length > 0) {
      setSelectedFiles(files)
      setShowUpload(true)
    }
  }, [])

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || []).filter(f => ALLOWED_TYPES.has(f.type) && f.size <= MAX_SIZE)
    if (files.length > 0) {
      setSelectedFiles(files)
      setShowUpload(true)
    }
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const handleUpload = async () => {
    for (const file of selectedFiles) {
      await uploadMutation.mutateAsync({
        file,
        patientId,
        category: category || undefined,
        description: description || undefined,
      })
    }
    setSelectedFiles([])
    setCategory('')
    setDescription('')
    setShowUpload(false)
  }

  const handleDelete = (id: number) => {
    if (confirm('Delete this attachment?')) {
      deleteMutation.mutate({ id, patientId })
    }
  }

  if (isLoading) return <PageSpinner />

  return (
    <div className="space-y-5">
      {/* Upload Area */}
      <div
        className={`card border-2 border-dashed transition-all duration-200 cursor-pointer ${
          dragging
            ? 'border-primary-400 bg-primary-50/50 shadow-lg scale-[1.01]'
            : 'border-gray-200 hover:border-primary-300 hover:bg-gray-50/50'
        }`}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => fileInputRef.current?.click()}
      >
        <div className="flex flex-col items-center justify-center py-10 gap-3">
          <div className={`w-14 h-14 rounded-2xl flex items-center justify-center transition-all duration-200 ${
            dragging ? 'bg-primary-100 scale-110' : 'bg-gray-100'
          }`}>
            <CloudUpload size={28} className={`transition-colors ${dragging ? 'text-primary-600' : 'text-gray-400'}`} />
          </div>
          <div className="text-center">
            <p className="font-semibold text-gray-700">
              {dragging ? 'Drop files here' : 'Drag & drop files or click to browse'}
            </p>
            <p className="text-xs text-gray-400 mt-1">
              Images, Videos, PDFs, Documents — Max 50 MB per file
            </p>
          </div>
        </div>
        <input
          ref={fileInputRef}
          type="file"
          multiple
          className="hidden"
          accept="image/*,video/*,application/pdf,.doc,.docx,.xls,.xlsx"
          onChange={handleFileSelect}
        />
      </div>

      {/* Upload Dialog */}
      {showUpload && (
        <div className="fixed inset-0 z-50 flex items-center justify-center" onClick={() => setShowUpload(false)}>
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
          <div className="relative bg-white rounded-2xl shadow-modal w-full max-w-md mx-4"
            onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between px-6 py-4 border-b border-border">
              <h2 className="font-bold text-gray-900">Upload Files</h2>
              <button className="btn-ghost p-1" onClick={() => setShowUpload(false)}>
                <X size={16} />
              </button>
            </div>
            <div className="px-6 py-5 space-y-4">
              {/* Selected files */}
              <div className="space-y-2">
                {selectedFiles.map((f, i) => (
                  <div key={i} className="flex items-center gap-3 p-3 bg-gray-50 rounded-xl">
                    {getFileIcon(f.type)}
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-800 truncate">{f.name}</p>
                      <p className="text-xs text-gray-400">{formatFileSize(f.size)}</p>
                    </div>
                    <button className="btn-ghost p-1" onClick={() =>
                      setSelectedFiles(prev => prev.filter((_, idx) => idx !== i))
                    }>
                      <X size={14} className="text-gray-400" />
                    </button>
                  </div>
                ))}
              </div>

              {/* Category */}
              <div>
                <label className="label">Category</label>
                <select className="input" value={category} onChange={e => setCategory(e.target.value)}>
                  <option value="">Select category...</option>
                  {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>

              {/* Description */}
              <div>
                <label className="label">Description (optional)</label>
                <input className="input" placeholder="Brief description..." value={description}
                  onChange={e => setDescription(e.target.value)} />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button className="btn-ghost" onClick={() => setShowUpload(false)}>Cancel</button>
                <button className="btn-primary" onClick={handleUpload}
                  disabled={uploadMutation.isPending || selectedFiles.length === 0}>
                  {uploadMutation.isPending ? <Spinner className="w-4 h-4" /> : (
                    <><Upload size={14} /> Upload {selectedFiles.length} file{selectedFiles.length !== 1 ? 's' : ''}</>
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* File Grid */}
      {(!data || data.length === 0) ? (
        <EmptyState
          title="No attachments yet"
          description="Upload test results, photos, videos, or documents for this patient"
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {data.map(file => (
            <div key={file.id}
              className="card group hover:shadow-lg transition-all duration-200 overflow-hidden">
              {/* Preview area */}
              <div
                className="relative h-36 bg-gradient-to-br from-gray-50 to-gray-100 flex items-center justify-center cursor-pointer overflow-hidden"
                onClick={() => canPreview(file.contentType) ? setPreviewFile(file) : attachmentsApi.download(file.id, file.fileName)}
              >
                {file.contentType.startsWith('image/') ? (
                  <img
                    src={attachmentsApi.getPreviewUrl(file.id)}
                    alt={file.fileName}
                    className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105"
                    loading="lazy"
                  />
                ) : (
                  <div className="flex flex-col items-center gap-2">
                    {getFileIconLarge(file.contentType)}
                    <span className="text-xs text-gray-400 font-medium uppercase">
                      {file.contentType.split('/').pop()?.replace('vnd.openxmlformats-officedocument.wordprocessingml.document', 'docx')
                        .replace('vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'xlsx')
                        .replace('vnd.ms-excel', 'xls')
                        .replace('msword', 'doc')}
                    </span>
                  </div>
                )}
                {/* Hover overlay */}
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-all duration-200 flex items-center justify-center">
                  <div className="opacity-0 group-hover:opacity-100 transition-opacity">
                    {canPreview(file.contentType) ? (
                      <div className="bg-white/90 backdrop-blur-sm rounded-full p-2 shadow-md">
                        <Eye size={18} className="text-gray-700" />
                      </div>
                    ) : (
                      <div className="bg-white/90 backdrop-blur-sm rounded-full p-2 shadow-md">
                        <Download size={18} className="text-gray-700" />
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Info area */}
              <div className="p-3.5">
                <p className="text-sm font-semibold text-gray-800 truncate" title={file.fileName}>
                  {file.fileName}
                </p>
                <div className="flex items-center gap-2 mt-1.5">
                  <span className="text-xs text-gray-400">{formatFileSize(file.fileSize)}</span>
                  {file.category && (
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold ${getCategoryColor(file.category)}`}>
                      {file.category}
                    </span>
                  )}
                </div>
                {file.description && (
                  <p className="text-xs text-gray-500 mt-1 line-clamp-1">{file.description}</p>
                )}
                <p className="text-[10px] text-gray-300 mt-2">{fmt.relative(file.createdAt)}</p>

                {/* Actions */}
                <div className="flex items-center gap-1 mt-2.5 pt-2.5 border-t border-gray-100">
                  {canPreview(file.contentType) && (
                    <button className="btn-ghost p-1.5 text-xs gap-1"
                      onClick={() => setPreviewFile(file)}>
                      <Eye size={13} /> Preview
                    </button>
                  )}
                  <button className="btn-ghost p-1.5 text-xs gap-1"
                    onClick={() => attachmentsApi.download(file.id, file.fileName)}>
                    <Download size={13} /> Download
                  </button>
                  <button className="btn-ghost p-1.5 text-xs gap-1 ml-auto text-red-400 hover:text-red-600 hover:bg-red-50"
                    onClick={() => handleDelete(file.id)}>
                    <Trash2 size={13} />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Preview Modal */}
      {previewFile && (
        <FilePreviewModal file={previewFile} onClose={() => setPreviewFile(null)} />
      )}
    </div>
  )
}

// ── Preview Modal ─────────────────────────────────────────────────────────────
function FilePreviewModal({ file, onClose }: { file: PatientAttachmentDto; onClose: () => void }) {
  const previewUrl = attachmentsApi.getPreviewUrl(file.id)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center" onClick={onClose}>
      <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" />
      <div className="relative bg-white rounded-2xl shadow-modal w-full max-w-5xl mx-4 max-h-[92vh] flex flex-col overflow-hidden"
        onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-3.5 border-b border-border flex-shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            {getFileIcon(file.contentType)}
            <div className="min-w-0">
              <p className="font-semibold text-gray-900 truncate">{file.fileName}</p>
              <div className="flex items-center gap-2 mt-0.5">
                <span className="text-xs text-gray-400">{formatFileSize(file.fileSize)}</span>
                {file.category && (
                  <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold ${getCategoryColor(file.category)}`}>
                    {file.category}
                  </span>
                )}
                <span className="text-xs text-gray-300">{fmt.relative(file.createdAt)}</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 flex-shrink-0 ml-4">
            <button className="btn-secondary text-xs"
              onClick={() => attachmentsApi.download(file.id, file.fileName)}>
              <Download size={14} /> Download
            </button>
            <button className="btn-ghost p-1.5" onClick={onClose}>
              <X size={18} />
            </button>
          </div>
        </div>

        {/* Preview content */}
        <div className="flex-1 overflow-auto bg-gray-900/5 flex items-center justify-center p-4 min-h-[400px]">
          {file.contentType.startsWith('image/') && (
            <img
              src={previewUrl}
              alt={file.fileName}
              className="max-w-full max-h-[75vh] object-contain rounded-lg shadow-lg"
            />
          )}

          {file.contentType.startsWith('video/') && (
            <video
              src={previewUrl}
              controls
              autoPlay={false}
              className="max-w-full max-h-[75vh] rounded-lg shadow-lg bg-black"
            >
              Your browser does not support video playback.
            </video>
          )}

          {file.contentType === 'application/pdf' && (
            <iframe
              src={previewUrl}
              title={file.fileName}
              className="w-full h-[75vh] rounded-lg shadow-lg bg-white border-0"
            />
          )}
        </div>

        {/* Description footer */}
        {file.description && (
          <div className="px-6 py-3 border-t border-border bg-gray-50 flex-shrink-0">
            <p className="text-xs text-gray-500"><span className="font-semibold">Description:</span> {file.description}</p>
          </div>
        )}
      </div>
    </div>
  )
}
