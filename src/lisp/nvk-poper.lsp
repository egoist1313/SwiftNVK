;;; nvk-poper.lsp — этап POPER: таблица отметок/длин/уклонов (не блок)
;;; Зависит от: nvk-config, nvk-dll, nvk-ep.

(defun nvk-stage-poper ( / r)
  (cond
    ((not (nvk-dll-est-p)) nil)
    (t
     (nvk-table-apply)
     (setq r (vl-catch-all-apply 'nvk-poper-draw nil))
     (cond
       ((vl-catch-all-error-p r) nil)
       ((null r) nil)
       (t r)))))

(princ "\n  nvk-poper.lsp")
(princ)
