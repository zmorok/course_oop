/* роль администратора бд svc_admin */
CREATE ROLE svc_admin
WITH LOGIN
  NOSUPERUSER
  NOINHERIT
  NOCREATEDB
  NOCREATEROLE
  NOREPLICATION
  PASSWORD 'svc_admin_125634'
  CONNECTION LIMIT 5;

/* роль для приложения svc_app */
CREATE ROLE svc_app
WITH LOGIN PASSWORD 'svc_app_password';

/* роль для пользователя бд svc_user */
CREATE ROLE svc_user NOLOGIN;

/* роль для пользователя приложения app_end_usr */
CREATE ROLE app_end_usr
WITH LOGIN PASSWORD 'svc_end_usr_125634'
INHERIT CONNECTION LIMIT 500;

GRANT svc_user TO app_end_usr;
GRANT svc_user TO svc_app;


/* база данных и схема */
CREATE DATABASE OOP_course_db WITH OWNER = svc_admin ENCODING = 'UTF8' TEMPLATE = template0;
CREATE SCHEMA core AUTHORIZATION svc_admin;
ALTER ROLE svc_admin   SET search_path = core;
ALTER ROLE svc_app     SET search_path = core;
ALTER ROLE app_end_usr SET search_path = core;

/* изъятие стандартных привилегий */
REVOKE ALL ON SCHEMA core FROM PUBLIC;
REVOKE ALL ON SCHEMA core FROM svc_app, svc_user, app_end_usr;

/* базовые привилегии на базу данных и схему */
GRANT CONNECT ON DATABASE OOP_course_db TO svc_admin, svc_app, svc_user, app_end_usr;
GRANT USAGE ON SCHEMA core TO svc_admin, svc_app, svc_user, app_end_usr;


/* привилегии администратора */
GRANT TEMPORARY ON DATABASE OOP_course_db TO svc_admin;
GRANT USAGE, CREATE ON SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL TABLES     IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES  IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS  IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL PROCEDURES IN SCHEMA core TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON TABLES     TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON SEQUENCES  TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON FUNCTIONS  TO svc_admin;
-- на процедуры нет дефолтных привилегий

/* ограничим доступ svc_admin к системным БД */
REVOKE CONNECT ON DATABASE postgres  FROM svc_admin;
REVOKE CONNECT ON DATABASE template1 FROM svc_admin;
REVOKE CONNECT ON DATABASE template0 FROM svc_admin;


/* для админ логов */
-- подлкючение к бд от postgres
SET ROLE "postgres";
ALTER PROCEDURE core.admin_import_audit_logs_json(text) OWNER TO postgres;
GRANT EXECUTE ON PROCEDURE core.admin_import_audit_logs_json(text) TO svc_admin;
SET ROLE "svc_admin";
-- далее от лица svc_admin

/* доступ приложению к ролям и пользователям в базе данных */
GRANT SELECT ON core.roles TO svc_app;
GRANT SELECT, INSERT ON core.users TO svc_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA core TO svc_app;


/* привилегии на представления для пользователя */
GRANT SELECT ON
  core.v_roles,
  core.v_users,
  core.v_projects,
  core.v_reviews,
  core.v_orders,
  core.v_order_extended,
  core.v_orders_archive,
  core.v_order_archive_extended,
  core.v_portfolio,
  core.v_orders_reviews,
  core.v_all_counterparts,
  core.v_complaints,
  core.v_user_notifications,
  core.v_user_warnings
TO svc_user;

/* привилегии на процедуры и фунции для пользователя */

-- системные утилиты пользователя
GRANT EXECUTE ON PROCEDURE core.update_user_last_online(integer, integer) TO svc_user;

-- профиль
GRANT EXECUTE ON PROCEDURE core.user_update_profile(integer, integer, char(128), varchar, varchar, varchar, varchar, varchar, varchar) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_profile(integer, integer, char(128), varchar, varchar, varchar, varchar, varchar, varchar, bytea) TO svc_user;

-- поиск/подбор
GRANT EXECUTE ON FUNCTION  core.search_users(integer, text) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.free_projects(integer, integer) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_counterparts(integer) TO svc_user;

-- проекты
GRANT EXECUTE ON PROCEDURE core.user_create_project(integer, integer, varchar, varchar, text, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_project(integer, integer, varchar, varchar, text, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_project(integer, integer) TO svc_user;

GRANT EXECUTE ON FUNCTION  core.user_get_projects_by_customer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_projects() TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_projects_by_status(varchar) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_project_by_id(integer) TO svc_user;

-- заказы
GRANT EXECUTE ON PROCEDURE core.user_create_order(integer, integer, integer, varchar, date) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_order(integer, integer, varchar, date)  TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_archive_order(integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_order(integer, integer) TO svc_user;

GRANT EXECUTE ON FUNCTION  core.user_get_orders_by_customer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_orders_by_freelancer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_orders() TO svc_user;

-- отзывы
GRANT EXECUTE ON PROCEDURE core.user_create_review(integer, integer, integer, text, integer, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_review(integer, integer, integer, text, integer, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_review(integer, integer, integer) TO svc_user;

GRANT EXECUTE ON FUNCTION  core.user_get_reviews(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_review_by_id(integer) TO svc_user;

-- жалобы
GRANT EXECUTE ON PROCEDURE core.user_create_complaint(integer, integer, integer, text, jsonb, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_complaint(integer, integer, text, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_complaint(integer, integer) TO svc_user;

GRANT EXECUTE ON FUNCTION  core.user_get_complaints(integer) TO svc_user;

-- портфолио
GRANT EXECUTE ON PROCEDURE core.user_create_portfolio(integer, integer, text, jsonb, text[], text) TO svc_user;
GRANT EXECUTE ON FUNCTION  core.user_get_portfolios(integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_portfolio(integer, integer, integer, text, jsonb, text[], text) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_portfolio(integer, integer, integer) TO svc_user;

-- инвайты
GRANT EXECUTE ON PROCEDURE core.user_send_project_invite(integer, integer, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_accept_invite(integer, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_decline_invite(integer, integer, integer) TO svc_user;