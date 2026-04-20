
Table "core"."roles" {
  "id_role" SERIAL [pk, increment]
  "role_name" VARCHAR(50) [unique, not null]
  "role_privileges" JSONB [not null, default: `'{}'::jsonb`]
}

Table "core"."users" {
  "id_user" SERIAL [pk, increment]
  "password" "CHAR (128)" [not null, constraint: `length(password) = 128`]
  "role" INTEGER [not null]
  "last_name" VARCHAR(100) [not null, constraint: `char_length(last_name)  > 0`]
  "first_name" VARCHAR(100) [not null, constraint: `char_length(first_name) > 0`]
  "middle_name" VARCHAR(100)
  "gender" VARCHAR(10) [constraint: `gender IN ('Male','Female','Other')`]
  "phone_number" VARCHAR(20) [constraint: `phone_number ~ '^\+?[0-9]{7,20}$'`]
  "email" VARCHAR(100) [unique, not null, constraint: `email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'`]
  "registration_date" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]
  "last_online_time" TIMESTAMP [constraint: `last_online_time IS NULL OR last_online_time >= registration_date`]
  "rating" NUMERIC(2,1) [constraint: `rating >= 0 AND rating <= 5`, default: 0.0]
  "photo" BYTEA [default: NULL]
}

Table "core"."portfolio" {
  "id_portfolio" SERIAL [pk, increment]
  "id_user" INTEGER [not null]
  "description" TEXT [not null, constraint: `char_length(description) > 0`]
  "media" JSONB
  "skills" "TEXT[]" [not null, constraint: `array_length(skills,1) >= 1`]
  "experience" TEXT
}

Table "core"."projects" {
  "id_project" SERIAL [pk, increment]
  "id_customer" INTEGER [not null]
  "title" VARCHAR(200) [not null, constraint: `char_length(title) > 0`]
  "status" VARCHAR(50) [not null, constraint: `status IN ('draft','open','in_progress','completed','cancelled')`]
  "description" TEXT [not null, constraint: `char_length(description) > 0`]
  "media" JSONB
}

Table "core"."notifications" {
  "id_notification" SERIAL [pk, increment]
  "id_sender" INT [not null]
  "id_receiver" INT [not null]
  "id_project" INT [not null]
  "type" VARCHAR(30) [not null, constraint: `type IN ('system','invite','status','message')`, default: 'system']
  "payload" JSONB
  "created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

  Indexes {
    (id_receiver, id_project) [unique, name: "uq_invite_per_project"]
  }
}

Table "core"."orders" {
  "id_order" SERIAL [pk, increment]
  "id_project" INTEGER [unique, not null]
  "id_freelancer" INTEGER [not null]
  "status" VARCHAR(50) [not null, constraint: `status IN ('pending','active','completed','cancelled','disputed')`]
  "creation_date" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]
  "deadline" DATE [constraint: `deadline IS NULL OR deadline::timestamp >= creation_date`]
}

Table "core"."orders_archive" {
  "id_order_arc" SERIAL [pk, increment]
  "id_order" INT [not null]
  "id_project" INT [not null]
  "project_title" TEXT [not null]
  "id_customer" INT [not null]
  "id_freelancer" INT
  "status" VARCHAR(50) [constraint: `status IN ('completed','cancelled')`]
  "creation_date" TIMESTAMP
  "deadline" DATE
}

Table "core"."reviews" {
  "id_review" SERIAL [pk, increment]
  "id_order" INT [not null]
  "id_author" INT [not null]
  "id_recipient" INT
  "comment" TEXT [not null, constraint: `char_length(comment) > 0`]
  "rating" INT [not null, constraint: `rating BETWEEN 1 AND 5`]
  "media" JSONB

  Constraints {
    `id_recipient IS NULL OR id_author <> id_recipient` [name: 'ck_no_self_review']
  }

  Indexes {
    (id_order, id_author) [unique, name: "uq_review_per_side"]
  }
}

Table "core"."complaints" {
  "id_complaint" SERIAL [pk, increment]
  "id_user" INTEGER [not null]
  "filed_by" INTEGER [not null]
  "processed_by_admin" INTEGER
  "status" VARCHAR(50) [not null, constraint: `status IN ('new','in_progress','resolved','dismissed')`]
  "description" TEXT [not null, constraint: `char_length(description) > 0`]
  "media" JSONB
  "id_order" INT
  "id_order_arc" INT

  Constraints {
    `id_order IS NOT NULL OR id_order_arc IS NOT NULL` [name: 'ck_complaint_link']
    `id_user <> filed_by` [name: 'ck_no_self_complaint']
  }
}

Table "core"."warnings" {
  "id_warning" SERIAL [pk, increment]
  "id_user" INT [not null]
  "issued_by_admin" INT [not null]
  "id_complaint" INT [unique]
  "message" TEXT [not null]
  "issued_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]
  "expires_at" TIMESTAMP
  "is_resolved" BOOLEAN [not null, default: FALSE]
}

Table "core"."audit_logs" {
  "id_log" SERIAL [pk, increment]
  "user_id" INTEGER
  "proc_name" VARCHAR(100)
  "action" VARCHAR(10) [not null, constraint: `action IN ('INSERT','UPDATE','DELETE')`]
  "table_name" VARCHAR(50) [not null]
  "record_id" INTEGER [not null]
  "old_data" JSONB
  "new_data" JSONB
  "changed_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]
}

Ref:"core"."roles"."id_role" < "core"."users"."role" [update: cascade, delete: restrict]

Ref:"core"."users"."id_user" < "core"."portfolio"."id_user" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."projects"."id_customer" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."notifications"."id_sender" [delete: cascade]

Ref:"core"."users"."id_user" < "core"."notifications"."id_receiver" [delete: cascade]

Ref:"core"."projects"."id_project" < "core"."notifications"."id_project" [delete: cascade]

Ref:"core"."projects"."id_project" < "core"."orders"."id_project" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."orders"."id_freelancer" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."orders_archive"."id_customer" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."orders_archive"."id_freelancer" [update: cascade, delete: cascade]

Ref:"core"."orders_archive"."id_order_arc" < "core"."reviews"."id_order" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."reviews"."id_author" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."reviews"."id_recipient" [update: cascade, delete: set null]

Ref:"core"."users"."id_user" < "core"."complaints"."id_user" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."complaints"."filed_by" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."complaints"."processed_by_admin" [update: cascade, delete: set null]

Ref:"core"."orders"."id_order" < "core"."complaints"."id_order" [update: cascade, delete: set null]

Ref:"core"."orders_archive"."id_order_arc" < "core"."complaints"."id_order_arc" [update: cascade, delete: set null]

Ref:"core"."users"."id_user" < "core"."warnings"."id_user" [update: cascade, delete: cascade]

Ref:"core"."users"."id_user" < "core"."warnings"."issued_by_admin" [update: cascade, delete: restrict]

Ref:"core"."complaints"."id_complaint" < "core"."warnings"."id_complaint" [update: cascade, delete: set null]

Ref:"core"."users"."id_user" < "core"."audit_logs"."user_id" [update: cascade, delete: set null]
