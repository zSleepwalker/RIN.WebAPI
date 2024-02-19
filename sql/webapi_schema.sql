--
-- PostgreSQL database dump
--

-- Dumped from database version 14.1
-- Dumped by pg_dump version 14.10

-- Started on 2024-02-18 22:03:25 UTC

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 6 (class 2615 OID 16385)
-- Name: webapi; Type: SCHEMA; Schema: -; Owner: tmwadmin
--

CREATE SCHEMA webapi;


ALTER SCHEMA webapi OWNER TO tmwadmin;

--
-- TOC entry 5260 (class 0 OID 0)
-- Dependencies: 6
-- Name: SCHEMA webapi; Type: COMMENT; Schema: -; Owner: tmwadmin
--

COMMENT ON SCHEMA webapi IS 'standard public schema';


--
-- TOC entry 825 (class 1255 OID 16386)
-- Name: CreateNewAccount(text, text, date, boolean, text, text, bytea); Type: FUNCTION; Schema: webapi; Owner: tmwadmin
--

CREATE FUNCTION webapi."CreateNewAccount"(email text, country text, birthday date, email_optin boolean, uid text, secret text, password_hash bytea, OUT error_text text, OUT new_account_id bigint) RETURNS record
    LANGUAGE plpgsql
    AS $$
declare
    c text;
BEGIN

    -- Check if the email is already in use, the constraint will do this but want to avoid incrementing the sequence on fails
    IF (SELECT exists (SELECT 1 FROM webapi."Accounts" WHERE "Accounts".email = "CreateNewAccount".email)) THEN
        new_account_id = -1;
        error_text = 'ERR_ACCOUNT_EXISTS';
        RETURN;
    END IF;

    INSERT INTO webapi."Accounts" (email, uid, password_hash, birthday, country, secret, email_optin,
                                   created_at, last_login, email_verified, is_dev, character_limit, rb_balance)
    VALUES (email, uid, password_hash, birthday, country, secret, email_optin,
            current_timestamp, '-infinity', false, false, -1, 0) RETURNING account_id INTO new_account_id;

    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS c := CONSTRAINT_NAME;
        IF c = 'accounts_email_uindex' THEN
            error_text = 'ERR_ACCOUNT_EXISTS';
        ELSE
            error_text = 'ERR_UNKNOWN';
            RAISE;
        END IF;
END
$$;


ALTER FUNCTION webapi."CreateNewAccount"(email text, country text, birthday date, email_optin boolean, uid text, secret text, password_hash bytea, OUT error_text text, OUT new_account_id bigint) OWNER TO tmwadmin;

--
-- TOC entry 5261 (class 0 OID 0)
-- Dependencies: 825
-- Name: FUNCTION "CreateNewAccount"(email text, country text, birthday date, email_optin boolean, uid text, secret text, password_hash bytea, OUT error_text text, OUT new_account_id bigint); Type: COMMENT; Schema: webapi; Owner: tmwadmin
--

COMMENT ON FUNCTION webapi."CreateNewAccount"(email text, country text, birthday date, email_optin boolean, uid text, secret text, password_hash bytea, OUT error_text text, OUT new_account_id bigint) IS 'Create a new account for a user and the default tables and data for it';


--
-- TOC entry 834 (class 1255 OID 16387)
-- Name: CreateNewCharacter(bigint, text, boolean, integer, integer, bytea); Type: FUNCTION; Schema: webapi; Owner: tmwadmin
--

CREATE FUNCTION webapi."CreateNewCharacter"(account_id bigint, name text, is_dev boolean, voice_setid integer, gender integer, visuals bytea, OUT error_text text, OUT new_character_id bigint) RETURNS record
    LANGUAGE plpgsql
    AS $$
declare
    c text;
BEGIN

    -- Check if the name isn't used
    IF (SELECT exists(SELECT 1 FROM webapi."Characters" WHERE webapi."Characters".name = "CreateNewCharacter".name)) THEN
        new_character_id = -1;
        error_text = 'ERR_NAME_IN_USE';
        RETURN;
    END IF;

    insert into webapi."Characters" (character_guid,
                                     name,
                                     unique_name,
                                     is_dev,
                                     is_active,
                                     account_id,
                                     created_at,
                                     title_id,
                                     time_played_secs,
                                     needs_name_change,
                                     gender,
                                     last_seen_at,
                                     race,
                                     visuals)
    values (webapi.create_entity_guid(254),
      name,
            UPPER(name),
            (is_dev AND (SELECT webapi."Accounts".is_dev FROM webapi."Accounts" WHERE webapi."Accounts".account_id = "CreateNewCharacter".account_id)),
            true,
            "CreateNewCharacter".account_id,
            current_timestamp,
            0,
            0,
            false,
            gender,
            current_timestamp,
            0,
            visuals)
    RETURNING character_guid INTO new_character_id;

    error_text = '';

END
$$;


ALTER FUNCTION webapi."CreateNewCharacter"(account_id bigint, name text, is_dev boolean, voice_setid integer, gender integer, visuals bytea, OUT error_text text, OUT new_character_id bigint) OWNER TO tmwadmin;

--
-- TOC entry 835 (class 1255 OID 16388)
-- Name: create_entity_guid(integer); Type: FUNCTION; Schema: webapi; Owner: tmwadmin
--

CREATE FUNCTION webapi.create_entity_guid("Type" integer) RETURNS bigint
    LANGUAGE plpgsql
    AS $$DECLARE
counter bigint;
serverId bigint;
timestamp bigint;
BEGIN

counter = nextval('webapi."FFUUID_Counter_Seq"'::regclass);
serverId = (1::bigint << 56);
timestamp = (((extract(epoch from pg_postmaster_start_time())::bigint >> 8) & x'00FFFFFF'::bigint << 32));
RETURN serverId + timestamp + (counter << 8) + "Type";

END$$;


ALTER FUNCTION webapi.create_entity_guid("Type" integer) OWNER TO tmwadmin;

--
-- TOC entry 5262 (class 0 OID 0)
-- Dependencies: 835
-- Name: FUNCTION create_entity_guid("Type" integer); Type: COMMENT; Schema: webapi; Owner: tmwadmin
--

COMMENT ON FUNCTION webapi.create_entity_guid("Type" integer) IS 'Create an entity id for example for a character, pass in a number for the type

Based on https://gist.github.com/SilentCLD/881839a9f45578f1618db012fc789a71 ';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 211 (class 1259 OID 16389)
-- Name: Accounts; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Accounts" (
    account_id bigint NOT NULL,
    is_dev boolean DEFAULT false NOT NULL,
    character_limit smallint,
    email text NOT NULL,
    uid text NOT NULL,
    password_hash bytea NOT NULL,
    created_at timestamp with time zone NOT NULL,
    last_login timestamp with time zone,
    birthday date NOT NULL,
    country character(2) NOT NULL,
    secret text NOT NULL,
    email_optin boolean DEFAULT false NOT NULL,
    email_verified boolean DEFAULT false NOT NULL,
    rb_balance bigint NOT NULL
);


ALTER TABLE webapi."Accounts" OWNER TO tmwadmin;

--
-- TOC entry 212 (class 1259 OID 16397)
-- Name: Accounts_account_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE webapi."Accounts" ALTER COLUMN account_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME webapi."Accounts_account_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 815 (class 1259 OID 19537)
-- Name: Army; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Army" (
    id bigint NOT NULL,
    account_id bigint DEFAULT 0 NOT NULL,
    army_guid bigint DEFAULT 0 NOT NULL,
    character_guid bigint DEFAULT 0 NOT NULL,
    name text NOT NULL,
    unique_name text NOT NULL,
    description text,
    playstyle text NOT NULL,
    personality text NOT NULL,
    motd text,
    is_recruiting boolean DEFAULT false NOT NULL,
    created_at bigint NOT NULL,
    updated_at bigint NOT NULL,
    commander_guid bigint DEFAULT 0 NOT NULL,
    tag_position smallint DEFAULT 1 NOT NULL,
    min_size smallint DEFAULT 1 NOT NULL,
    max_size smallint DEFAULT 50 NOT NULL,
    disbanded boolean DEFAULT false NOT NULL,
    website text,
    mass_email boolean DEFAULT true NOT NULL,
    region text NOT NULL,
    login_message text,
    timezone text DEFAULT (+ 0) NOT NULL,
    established_at bigint DEFAULT 0 NOT NULL,
    tag text NOT NULL,
    language text NOT NULL
);


ALTER TABLE webapi."Army" OWNER TO tmwadmin;

--
-- TOC entry 814 (class 1259 OID 19536)
-- Name: Armies_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."Armies_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."Armies_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5263 (class 0 OID 0)
-- Dependencies: 814
-- Name: Armies_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."Armies_id_seq" OWNED BY webapi."Army".id;


--
-- TOC entry 821 (class 1259 OID 19604)
-- Name: ArmyApplications; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ArmyApplications" (
    id bigint NOT NULL,
    army_id bigint NOT NULL,
    army_guid bigint DEFAULT 0 NOT NULL,
    character_guid bigint NOT NULL,
    message text NOT NULL,
    direction text NOT NULL,
    created_at bigint DEFAULT 0 NOT NULL,
    updated_at bigint DEFAULT 0 NOT NULL,
    invite boolean DEFAULT false
);


ALTER TABLE webapi."ArmyApplications" OWNER TO tmwadmin;

--
-- TOC entry 820 (class 1259 OID 19603)
-- Name: ArmyApplications_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ArmyApplications_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ArmyApplications_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5264 (class 0 OID 0)
-- Dependencies: 820
-- Name: ArmyApplications_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ArmyApplications_id_seq" OWNED BY webapi."ArmyApplications".id;


--
-- TOC entry 819 (class 1259 OID 19585)
-- Name: ArmyMembers; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ArmyMembers" (
    id bigint NOT NULL,
    army_id bigint DEFAULT 0 NOT NULL,
    army_guid bigint DEFAULT 0 NOT NULL,
    character_guid bigint DEFAULT 0 NOT NULL,
    army_rank_id bigint DEFAULT 0 NOT NULL,
    created_at bigint DEFAULT 0 NOT NULL,
    updated_at bigint DEFAULT 0 NOT NULL,
    public_note text NOT NULL,
    officer_note text NOT NULL
);


ALTER TABLE webapi."ArmyMembers" OWNER TO tmwadmin;

--
-- TOC entry 818 (class 1259 OID 19584)
-- Name: ArmyMembers_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ArmyMembers_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ArmyMembers_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5265 (class 0 OID 0)
-- Dependencies: 818
-- Name: ArmyMembers_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ArmyMembers_id_seq" OWNED BY webapi."ArmyMembers".id;


--
-- TOC entry 817 (class 1259 OID 19562)
-- Name: ArmyRanks; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ArmyRanks" (
    id bigint NOT NULL,
    army_id bigint DEFAULT 0 NOT NULL,
    army_guid bigint DEFAULT 0 NOT NULL,
    name text NOT NULL,
    is_commander boolean DEFAULT false NOT NULL,
    can_invite boolean DEFAULT false NOT NULL,
    can_kick boolean DEFAULT false NOT NULL,
    created_at bigint DEFAULT 0 NOT NULL,
    updated_at bigint DEFAULT 0 NOT NULL,
    can_edit boolean DEFAULT false NOT NULL,
    can_promote boolean DEFAULT false NOT NULL,
    "position" smallint DEFAULT 1 NOT NULL,
    is_officer boolean DEFAULT false NOT NULL,
    can_edit_motd boolean DEFAULT false NOT NULL,
    can_mass_email boolean DEFAULT false NOT NULL,
    is_default boolean DEFAULT false NOT NULL
);


ALTER TABLE webapi."ArmyRanks" OWNER TO tmwadmin;

--
-- TOC entry 816 (class 1259 OID 19561)
-- Name: ArmyRanks_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ArmyRanks_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ArmyRanks_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5266 (class 0 OID 0)
-- Dependencies: 816
-- Name: ArmyRanks_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ArmyRanks_id_seq" OWNED BY webapi."ArmyRanks".id;


--
-- TOC entry 213 (class 1259 OID 16398)
-- Name: Battleframes; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Battleframes" (
    id bigint NOT NULL,
    character_guid bigint NOT NULL,
    battleframe_sdb_id integer NOT NULL,
    visuals bytea NOT NULL,
    hidden boolean DEFAULT false NOT NULL,
    level integer DEFAULT 1 NOT NULL,
    xp bigint DEFAULT 0 NOT NULL
);


ALTER TABLE webapi."Battleframes" OWNER TO tmwadmin;

--
-- TOC entry 214 (class 1259 OID 16406)
-- Name: Battleframes_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE webapi."Battleframes" ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME webapi."Battleframes_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 215 (class 1259 OID 16407)
-- Name: Characters; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Characters" (
    character_guid bigint NOT NULL,
    name text NOT NULL,
    unique_name text NOT NULL,
    is_dev boolean DEFAULT false NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    account_id bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    title_id integer,
    time_played_secs integer,
    needs_name_change boolean DEFAULT false NOT NULL,
    gender smallint DEFAULT 0 NOT NULL,
    last_seen_at timestamp with time zone NOT NULL,
    race smallint NOT NULL,
    visuals bytea NOT NULL,
    deleted_at timestamp with time zone,
    expires_in timestamp with time zone,
    current_battleframe_guid bigint
);


ALTER TABLE webapi."Characters" OWNER TO tmwadmin;

--
-- TOC entry 216 (class 1259 OID 16416)
-- Name: ClientEvents; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ClientEvents" (
    id bigint NOT NULL,
    event smallint,
    action text,
    message text,
    source text,
    user_id bigint,
    data text,
    date timestamp with time zone
);


ALTER TABLE webapi."ClientEvents" OWNER TO tmwadmin;

--
-- TOC entry 217 (class 1259 OID 16421)
-- Name: ClientEvents_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE webapi."ClientEvents" ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME webapi."ClientEvents_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 218 (class 1259 OID 16422)
-- Name: Costs; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Costs" (
    id bigint NOT NULL,
    name text NOT NULL,
    price integer NOT NULL,
    description text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


ALTER TABLE webapi."Costs" OWNER TO tmwadmin;

--
-- TOC entry 219 (class 1259 OID 16427)
-- Name: DeletionQueue; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."DeletionQueue" (
    character_guid bigint NOT NULL,
    account_id bigint NOT NULL,
    deleted_at timestamp with time zone NOT NULL,
    expires_in timestamp with time zone NOT NULL
);


ALTER TABLE webapi."DeletionQueue" OWNER TO tmwadmin;

--
-- TOC entry 220 (class 1259 OID 16430)
-- Name: FFUUID_Counter_Seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."FFUUID_Counter_Seq"
    START WITH 0
    INCREMENT BY 1
    MINVALUE 0
    MAXVALUE 16777215
    CACHE 1;


ALTER TABLE webapi."FFUUID_Counter_Seq" OWNER TO tmwadmin;

--
-- TOC entry 221 (class 1259 OID 16431)
-- Name: LoginEvents; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."LoginEvents" (
    id bigint NOT NULL,
    name text,
    description text,
    color text,
    is_active boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


ALTER TABLE webapi."LoginEvents" OWNER TO tmwadmin;

--
-- TOC entry 222 (class 1259 OID 16437)
-- Name: Purchases; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."Purchases" (
    account_id bigint NOT NULL,
    purchase_id bigint NOT NULL
);


ALTER TABLE webapi."Purchases" OWNER TO tmwadmin;

--
-- TOC entry 223 (class 1259 OID 16440)
-- Name: Purchases_account_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE webapi."Purchases" ALTER COLUMN account_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME webapi."Purchases_account_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 224 (class 1259 OID 16441)
-- Name: VipData; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."VipData" (
    account_id bigint NOT NULL,
    start_date timestamp with time zone NOT NULL,
    expiration_date timestamp with time zone NOT NULL
);


ALTER TABLE webapi."VipData" OWNER TO tmwadmin;

--
-- TOC entry 5267 (class 0 OID 0)
-- Dependencies: 224
-- Name: TABLE "VipData"; Type: COMMENT; Schema: webapi; Owner: tmwadmin
--

COMMENT ON TABLE webapi."VipData" IS 'vip data, if the user has vip they should have a row here';


--
-- TOC entry 225 (class 1259 OID 16444)
-- Name: ZoneCertificates; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ZoneCertificates" (
    id integer NOT NULL,
    cert_id smallint NOT NULL,
    zone_setting_id smallint NOT NULL,
    authorize_position text NOT NULL,
    difficulty_key text,
    presence text NOT NULL
);


ALTER TABLE webapi."ZoneCertificates" OWNER TO tmwadmin;

--
-- TOC entry 226 (class 1259 OID 16449)
-- Name: ZoneCertificates_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ZoneCertificates_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ZoneCertificates_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5268 (class 0 OID 0)
-- Dependencies: 226
-- Name: ZoneCertificates_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ZoneCertificates_id_seq" OWNED BY webapi."ZoneCertificates".id;


--
-- TOC entry 227 (class 1259 OID 16450)
-- Name: ZoneDifficulty; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ZoneDifficulty" (
    id integer NOT NULL,
    zone_setting_id smallint NOT NULL,
    difficulty_key text NOT NULL,
    ui_string text NOT NULL,
    display_level smallint NOT NULL,
    min_level smallint NOT NULL,
    max_suggested_level smallint NOT NULL,
    min_players smallint NOT NULL,
    max_players smallint NOT NULL,
    min_players_accept smallint NOT NULL,
    group_min_players smallint NOT NULL,
    group_max_players smallint NOT NULL
);


ALTER TABLE webapi."ZoneDifficulty" OWNER TO tmwadmin;

--
-- TOC entry 228 (class 1259 OID 16455)
-- Name: ZoneDifficulty_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ZoneDifficulty_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ZoneDifficulty_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5269 (class 0 OID 0)
-- Dependencies: 228
-- Name: ZoneDifficulty_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ZoneDifficulty_id_seq" OWNED BY webapi."ZoneDifficulty".id;


--
-- TOC entry 229 (class 1259 OID 16456)
-- Name: ZoneSettings; Type: TABLE; Schema: webapi; Owner: tmwadmin
--

CREATE TABLE webapi."ZoneSettings" (
    id integer NOT NULL,
    zone_id integer DEFAULT 0 NOT NULL,
    mission_id smallint DEFAULT 0 NOT NULL,
    gametype text NOT NULL,
    instance_type_pool text NOT NULL,
    is_preview_zone boolean DEFAULT false NOT NULL,
    displayed_name text NOT NULL,
    displayed_desc text NOT NULL,
    description text NOT NULL,
    displayed_gametype text NOT NULL,
    cert_required boolean DEFAULT false NOT NULL,
    xp_bonus smallint DEFAULT 0 NOT NULL,
    sort_order smallint,
    rotation_priority smallint DEFAULT 1 NOT NULL,
    skip_matchmaking boolean DEFAULT true NOT NULL,
    queueing_enabled boolean DEFAULT true NOT NULL,
    team_count smallint DEFAULT 1 NOT NULL,
    min_players_per_team smallint DEFAULT 1 NOT NULL,
    max_players_per_team smallint DEFAULT 5 NOT NULL,
    min_players_accept_per_team smallint DEFAULT 0 NOT NULL,
    challenge_enabled boolean DEFAULT false NOT NULL,
    challenge_min_players_per_team smallint DEFAULT 0 NOT NULL,
    challenge_max_players_per_team smallint DEFAULT 0 NOT NULL,
    is_active boolean DEFAULT false NOT NULL,
    images text
);


ALTER TABLE webapi."ZoneSettings" OWNER TO tmwadmin;

--
-- TOC entry 230 (class 1259 OID 16477)
-- Name: ZoneSettings_id_seq; Type: SEQUENCE; Schema: webapi; Owner: tmwadmin
--

CREATE SEQUENCE webapi."ZoneSettings_id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE webapi."ZoneSettings_id_seq" OWNER TO tmwadmin;

--
-- TOC entry 5270 (class 0 OID 0)
-- Dependencies: 230
-- Name: ZoneSettings_id_seq; Type: SEQUENCE OWNED BY; Schema: webapi; Owner: tmwadmin
--

ALTER SEQUENCE webapi."ZoneSettings_id_seq" OWNED BY webapi."ZoneSettings".id;


--
-- TOC entry 5032 (class 2604 OID 19726)
-- Name: Army id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Army" ALTER COLUMN id SET DEFAULT nextval('webapi."Armies_id_seq"'::regclass);


--
-- TOC entry 5059 (class 2604 OID 19767)
-- Name: ArmyApplications id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyApplications" ALTER COLUMN id SET DEFAULT nextval('webapi."ArmyApplications_id_seq"'::regclass);


--
-- TOC entry 5053 (class 2604 OID 19787)
-- Name: ArmyMembers id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers" ALTER COLUMN id SET DEFAULT nextval('webapi."ArmyMembers_id_seq"'::regclass);


--
-- TOC entry 5047 (class 2604 OID 19820)
-- Name: ArmyRanks id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyRanks" ALTER COLUMN id SET DEFAULT nextval('webapi."ArmyRanks_id_seq"'::regclass);


--
-- TOC entry 5002 (class 2604 OID 16478)
-- Name: ZoneCertificates id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneCertificates" ALTER COLUMN id SET DEFAULT nextval('webapi."ZoneCertificates_id_seq"'::regclass);


--
-- TOC entry 5003 (class 2604 OID 16479)
-- Name: ZoneDifficulty id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneDifficulty" ALTER COLUMN id SET DEFAULT nextval('webapi."ZoneDifficulty_id_seq"'::regclass);


--
-- TOC entry 5020 (class 2604 OID 16480)
-- Name: ZoneSettings id; Type: DEFAULT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneSettings" ALTER COLUMN id SET DEFAULT nextval('webapi."ZoneSettings_id_seq"'::regclass);


--
-- TOC entry 5096 (class 2606 OID 19728)
-- Name: Army Armies_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Army"
    ADD CONSTRAINT "Armies_pkey" PRIMARY KEY (id);


--
-- TOC entry 5104 (class 2606 OID 19769)
-- Name: ArmyApplications ArmyApplications_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyApplications"
    ADD CONSTRAINT "ArmyApplications_pkey" PRIMARY KEY (id);


--
-- TOC entry 5098 (class 2606 OID 19646)
-- Name: Army ArmyGuid_ukey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Army"
    ADD CONSTRAINT "ArmyGuid_ukey" UNIQUE (army_guid);


--
-- TOC entry 5102 (class 2606 OID 19789)
-- Name: ArmyMembers ArmyMembers_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers"
    ADD CONSTRAINT "ArmyMembers_pkey" PRIMARY KEY (id);


--
-- TOC entry 5100 (class 2606 OID 19822)
-- Name: ArmyRanks ArmyRanks_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyRanks"
    ADD CONSTRAINT "ArmyRanks_pkey" PRIMARY KEY (id);


--
-- TOC entry 5067 (class 2606 OID 16482)
-- Name: Battleframes Character and Battleframe Type; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Battleframes"
    ADD CONSTRAINT "Character and Battleframe Type" UNIQUE (character_guid, battleframe_sdb_id);


--
-- TOC entry 5074 (class 2606 OID 16484)
-- Name: ClientEvents ClientEvents_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ClientEvents"
    ADD CONSTRAINT "ClientEvents_pkey" PRIMARY KEY (id);


--
-- TOC entry 5076 (class 2606 OID 16486)
-- Name: Costs Costs_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Costs"
    ADD CONSTRAINT "Costs_pkey" PRIMARY KEY (id);


--
-- TOC entry 5078 (class 2606 OID 16488)
-- Name: DeletionQueue DeletionQueue_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."DeletionQueue"
    ADD CONSTRAINT "DeletionQueue_pkey" PRIMARY KEY (character_guid);


--
-- TOC entry 5083 (class 2606 OID 16490)
-- Name: Purchases Purchases_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Purchases"
    ADD CONSTRAINT "Purchases_pkey" PRIMARY KEY (account_id);


--
-- TOC entry 5090 (class 2606 OID 16492)
-- Name: ZoneCertificates ZoneCertificates_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneCertificates"
    ADD CONSTRAINT "ZoneCertificates_pkey" PRIMARY KEY (id);


--
-- TOC entry 5092 (class 2606 OID 16494)
-- Name: ZoneDifficulty ZoneDifficulty_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneDifficulty"
    ADD CONSTRAINT "ZoneDifficulty_pkey" PRIMARY KEY (id);


--
-- TOC entry 5094 (class 2606 OID 16496)
-- Name: ZoneSettings ZoneSettings_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ZoneSettings"
    ADD CONSTRAINT "ZoneSettings_pkey" PRIMARY KEY (id);


--
-- TOC entry 5064 (class 2606 OID 16498)
-- Name: Accounts accounts_pk; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Accounts"
    ADD CONSTRAINT accounts_pk PRIMARY KEY (account_id);


--
-- TOC entry 5069 (class 2606 OID 16500)
-- Name: Battleframes battleframes_pkey; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Battleframes"
    ADD CONSTRAINT battleframes_pkey PRIMARY KEY (id);


--
-- TOC entry 5072 (class 2606 OID 16502)
-- Name: Characters characters_pk; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Characters"
    ADD CONSTRAINT characters_pk PRIMARY KEY (character_guid);


--
-- TOC entry 5081 (class 2606 OID 16504)
-- Name: LoginEvents login_events_pk; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."LoginEvents"
    ADD CONSTRAINT login_events_pk PRIMARY KEY (id);


--
-- TOC entry 5088 (class 2606 OID 16506)
-- Name: VipData vip_data_pk; Type: CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."VipData"
    ADD CONSTRAINT vip_data_pk PRIMARY KEY (account_id);


--
-- TOC entry 5061 (class 1259 OID 16507)
-- Name: accounts_account_id_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX accounts_account_id_uindex ON webapi."Accounts" USING btree (account_id);


--
-- TOC entry 5062 (class 1259 OID 16508)
-- Name: accounts_email_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX accounts_email_uindex ON webapi."Accounts" USING btree (email);


--
-- TOC entry 5065 (class 1259 OID 16509)
-- Name: accounts_uid_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX accounts_uid_uindex ON webapi."Accounts" USING btree (uid);


--
-- TOC entry 5070 (class 1259 OID 16510)
-- Name: characters_name_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX characters_name_uindex ON webapi."Characters" USING btree (name);


--
-- TOC entry 5079 (class 1259 OID 16511)
-- Name: login_events_id_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX login_events_id_uindex ON webapi."LoginEvents" USING btree (id);


--
-- TOC entry 5084 (class 1259 OID 16512)
-- Name: purchases_account_id_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX purchases_account_id_uindex ON webapi."Purchases" USING btree (account_id);


--
-- TOC entry 5085 (class 1259 OID 16513)
-- Name: purchases_purchase_id_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX purchases_purchase_id_uindex ON webapi."Purchases" USING btree (purchase_id);


--
-- TOC entry 5086 (class 1259 OID 16514)
-- Name: vip_data_account_id_uindex; Type: INDEX; Schema: webapi; Owner: tmwadmin
--

CREATE UNIQUE INDEX vip_data_account_id_uindex ON webapi."VipData" USING btree (account_id);


--
-- TOC entry 5114 (class 2606 OID 19647)
-- Name: ArmyApplications ArmyApplications_army_guid_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyApplications"
    ADD CONSTRAINT "ArmyApplications_army_guid_fkey" FOREIGN KEY (army_guid) REFERENCES webapi."Army"(army_guid) NOT VALID;


--
-- TOC entry 5110 (class 2606 OID 19652)
-- Name: ArmyMembers ArmyGuid_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers"
    ADD CONSTRAINT "ArmyGuid_fkey" FOREIGN KEY (army_guid) REFERENCES webapi."Army"(army_guid) NOT VALID;


--
-- TOC entry 5107 (class 2606 OID 19657)
-- Name: ArmyRanks ArmyGuid_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyRanks"
    ADD CONSTRAINT "ArmyGuid_fkey" FOREIGN KEY (army_guid) REFERENCES webapi."Army"(army_guid) NOT VALID;


--
-- TOC entry 5115 (class 2606 OID 19776)
-- Name: ArmyApplications ArmyId_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyApplications"
    ADD CONSTRAINT "ArmyId_fkey" FOREIGN KEY (army_id) REFERENCES webapi."Army"(id) NOT VALID;


--
-- TOC entry 5111 (class 2606 OID 19797)
-- Name: ArmyMembers ArmyId_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers"
    ADD CONSTRAINT "ArmyId_fkey" FOREIGN KEY (army_id) REFERENCES webapi."Army"(id) NOT VALID;


--
-- TOC entry 5108 (class 2606 OID 19835)
-- Name: ArmyRanks ArmyId_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyRanks"
    ADD CONSTRAINT "ArmyId_fkey" FOREIGN KEY (army_id) REFERENCES webapi."Army"(id) NOT VALID;


--
-- TOC entry 5112 (class 2606 OID 19823)
-- Name: ArmyMembers ArmyRankId_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers"
    ADD CONSTRAINT "ArmyRankId_fkey" FOREIGN KEY (army_rank_id) REFERENCES webapi."ArmyRanks"(id) NOT VALID;


--
-- TOC entry 5105 (class 2606 OID 16515)
-- Name: Battleframes Char ID; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."Battleframes"
    ADD CONSTRAINT "Char ID" FOREIGN KEY (character_guid) REFERENCES webapi."Characters"(character_guid) NOT VALID;


--
-- TOC entry 5109 (class 2606 OID 19625)
-- Name: ArmyMembers CharacterGuid_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyMembers"
    ADD CONSTRAINT "CharacterGuid_fkey" FOREIGN KEY (character_guid) REFERENCES webapi."Characters"(character_guid) NOT VALID;


--
-- TOC entry 5113 (class 2606 OID 19635)
-- Name: ArmyApplications CharacterGuid_fkey; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."ArmyApplications"
    ADD CONSTRAINT "CharacterGuid_fkey" FOREIGN KEY (character_guid) REFERENCES webapi."Characters"(character_guid) NOT VALID;


--
-- TOC entry 5106 (class 2606 OID 16520)
-- Name: VipData vipdata_accounts_account_id_fk; Type: FK CONSTRAINT; Schema: webapi; Owner: tmwadmin
--

ALTER TABLE ONLY webapi."VipData"
    ADD CONSTRAINT vipdata_accounts_account_id_fk FOREIGN KEY (account_id) REFERENCES webapi."Accounts"(account_id) ON DELETE CASCADE;


-- Completed on 2024-02-18 22:03:25 UTC

--
-- PostgreSQL database dump complete
--

